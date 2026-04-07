using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>InvoiceService.ts</c>.</summary>
    public interface IInvoiceService
    {
        Task<ApiResponse<PaginatedResponse<InvoiceResponseDto>>> GetAllInvoices(int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<InvoiceResponseDto>>> GetAll();
        Task<ApiResponse<InvoiceResponseDto>> GetById(int id);
        Task<ApiResponse<List<InvoiceResponseDto>>> GetInvoicesByCustomer(int customerId);
        Task<ApiResponse<List<InvoiceResponseDto>>> GetInvoicesByCustomerCode(string customerCode);
        Task<ApiResponse<List<InvoiceResponseDto>>> GetByStaffId(int staffId);
        Task<ApiResponse<InvoiceResponseDto>> CreateInvoice(CreateInvoiceDto dto);
        Task<ApiResponse<InvoiceResponseDto>> UpdateInvoice(int id, UpdateInvoiceDto dto);
        Task<ApiResponse<bool>> DeleteInvoice(int id);
        Task<ApiResponse<List<InvoiceTimelineEntryDto>>> GetTimeline(int invoiceId);
        Task<ApiResponse<InvoiceTimelineEntryDto>> AddTimelineEntry(int invoiceId, AddTimelineEntryDto dto);
    }

    public class InvoiceService : IInvoiceService
    {
        IDbProvider dbprovider;

        public InvoiceService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        /// <summary>Uses <see cref="Service.CreatedBy"/> as <see cref="Invoice.StaffId"/> when that row exists in <c>users</c>.</summary>
        private async Task<int?> ResolveStaffIdFromServiceCreatedByAsync(long? serviceCreatedBy)
        {
            if (serviceCreatedBy is null || serviceCreatedBy <= 0 || serviceCreatedBy > int.MaxValue)
                return null;
            var uid = (int)serviceCreatedBy;
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var cmd = db.GetCommand("SELECT 1 FROM users WHERE id=@id LIMIT 1;");
                db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = uid;
                using (DbDataReader r = await db.Execute(cmd))
                    return await r.ReadAsync() ? uid : null;
            }
        }

        private static string? FormatUserDisplayName(User u)
        {
            var n = $"{u.FirstName} {u.LastName}".Trim();
            if (!string.IsNullOrEmpty(n)) return n;
            return string.IsNullOrEmpty(u.Email) ? null : u.Email;
        }

        private async Task<IReadOnlyDictionary<long, string>> ResolveUserDisplayNamesByIdAsync(IEnumerable<long?> userIds)
        {
            var intIds = userIds
                .Where(id => id.HasValue && id.Value > 0 && id.Value <= int.MaxValue)
                .Select(id => (int)id!.Value)
                .Distinct()
                .ToList();
            if (intIds.Count == 0)
                return new Dictionary<long, string>();

            var dict = new Dictionary<long, string>();
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var names = intIds.Select((_, i) => $"@u{i}").ToList();
                var cmd = db.GetCommand($@"
SELECT id, first_name, last_name, email
FROM users
WHERE id IN ({string.Join(", ", names)});");
                for (int i = 0; i < intIds.Count; i++)
                    db.AddParameter(cmd, $"u{i}", DbTypes.Types.Integer).Value = intIds[i];

                using (DbDataReader r = await db.Execute(cmd))
                {
                    while (await r.ReadAsync())
                    {
                        var id = r.GetInt32(r.GetOrdinal("id"));
                        var u = new User
                        {
                            Id = id,
                            FirstName = r.GetString(r.GetOrdinal("first_name")),
                            LastName = r.GetString(r.GetOrdinal("last_name")),
                            Email = r.GetString(r.GetOrdinal("email")),
                        };
                        var label = FormatUserDisplayName(u);
                        dict[(long)id] = string.IsNullOrEmpty(label) ? $"User #{id}" : label;
                    }
                }
            }

            return dict;
        }

        private async Task EnrichInvoicesAsync(List<InvoiceResponseDto> rows)
        {
            if (rows.Count == 0) return;
            var ids = rows.Select(r => r.CustomerId).Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return;
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var paramNames = ids.Select((_, i) => $"@c{i}").ToList();
                var cmd = db.GetCommand($@"
SELECT id, code
FROM customers
WHERE id IN ({string.Join(", ", paramNames)});");
                for (int i = 0; i < ids.Count; i++)
                    db.AddParameter(cmd, $"c{i}", DbTypes.Types.Integer).Value = ids[i];

                var map = new Dictionary<int, string?>();
                using (DbDataReader r = await db.Execute(cmd))
                {
                    while (await r.ReadAsync())
                        map[r.GetInt32(r.GetOrdinal("id"))] = r.IsDBNull(r.GetOrdinal("code")) ? null : r.GetString(r.GetOrdinal("code"));
                }

                foreach (var row in rows)
                    row.CustomerCode = map.GetValueOrDefault(row.CustomerId);
            }
        }

        /// <summary>
        /// Keep customer sales milestone flags in sync with invoice/payment activity.
        /// - Any invoice created => InvoiceGenerated = true
        /// - Any received amount > 0 => AdvancePaymentReceived = true
        /// </summary>
        private async Task SyncCustomerSalesFlagsFromInvoiceAsync(string customerCode, string invoiceNumber, decimal received)
        {
            if (string.IsNullOrWhiteSpace(customerCode)) return;
            var cc = customerCode.Trim();
            var invNo = string.IsNullOrWhiteSpace(invoiceNumber) ? null : invoiceNumber.Trim();
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var now = DateTime.UtcNow;
                var update = db.GetCommand(@"
UPDATE customers
SET
    invoice_generated = true,
    invoice_number = CASE
        WHEN @invoice_number IS NULL OR @invoice_number = '' THEN invoice_number
        ELSE @invoice_number
    END,
    advance_payment_received = CASE
        WHEN @received > 0 THEN true
        ELSE advance_payment_received
    END,
    modified_at = @modified_at,
    modified_by = @modified_by
WHERE code = @code;");
                db.AddParameter(update, "invoice_number", DbTypes.Types.String).Value = invNo ?? (object)DBNull.Value;
                db.AddParameter(update, "received", DbTypes.Types.Decimal).Value = received;
                db.AddParameter(update, "modified_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(update, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                db.AddParameter(update, "code", DbTypes.Types.String).Value = cc;
                await db.ExecuteNonQuery(update);
            }
        }

        private InvoiceResponseDto MapInvoice(Invoice i, IReadOnlyDictionary<long, string>? createdByLookup = null)
        {
            string? createdByName = null;
            if (i.CreatedBy is long cid && cid > 0 && createdByLookup != null && createdByLookup.TryGetValue(cid, out var resolved))
                createdByName = resolved;

            static string NormalizeInfinity(DateTime dt)
            {
                // Npgsql maps PostgreSQL +/-infinity to DateTime.MinValue/MaxValue by default.
                if (dt == DateTime.MinValue || dt == DateTime.MaxValue) return "";
                return dt.ToString("O");
            }

            return new InvoiceResponseDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                CustomerId = i.Customer?.Id ?? i.CustomerId,
                ServiceId = i.ServiceId,
                StaffId = i.StaffId,
                PaymentModeId = i.PaymentModeId,
                PaymentStatusId = i.PaymentStatusId,
                Receivable = i.Receivable,
                Received = i.Received,
                SubscriptionStartAt = NormalizeInfinity(i.SubscriptionStartAt),
                SubscriptionEndAt = NormalizeInfinity(i.SubscriptionEndAt),
                IsActive = i.IsActive,
                CreatedAt = i.CreatedAt,
                CreatedBy = i.CreatedBy,
                CreatedByName = createdByName,
                ModifiedAt = i.ModifiedAt,
                ModifiedBy = i.ModifiedBy,
                PaidAt = i.PaidAt,
                PaidBy = i.PaidBy
            };
        }

        private static Invoice ReadInvoice(DbDataReader r)
        {
            return new Invoice
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                InvoiceNumber = r.GetString(r.GetOrdinal("invoice_number")),
                CustomerId = r.GetInt32(r.GetOrdinal("customer_id")),
                CustomerCode = r.GetString(r.GetOrdinal("customer_code")),
                ServiceId = r.GetInt32(r.GetOrdinal("service_id")),
                StaffId = r.IsDBNull(r.GetOrdinal("staff_id")) ? null : r.GetInt32(r.GetOrdinal("staff_id")),
                PaymentModeId = r.GetInt32(r.GetOrdinal("payment_mode_id")),
                PaymentStatusId = r.GetInt32(r.GetOrdinal("payment_status_id")),
                Receivable = r.GetDecimal(r.GetOrdinal("receivable")),
                Received = r.GetDecimal(r.GetOrdinal("received")),
                SubscriptionStartAt = r.GetDateTime(r.GetOrdinal("subscription_start_at")),
                SubscriptionEndAt = r.GetDateTime(r.GetOrdinal("subscription_end_at")),
                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                CreatedBy = r.IsDBNull(r.GetOrdinal("created_by")) ? null : r.GetInt64(r.GetOrdinal("created_by")),
                PaidAt = r.IsDBNull(r.GetOrdinal("paid_at")) ? null : r.GetDateTime(r.GetOrdinal("paid_at")),
                PaidBy = r.IsDBNull(r.GetOrdinal("paid_by")) ? null : r.GetString(r.GetOrdinal("paid_by")),
                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
            };
        }

        private async Task<int?> GetCustomerIdByCodeAsync(IDb db, string customerCode)
        {
            var cmd = db.GetCommand("SELECT id FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "code", DbTypes.Types.String).Value = customerCode.Trim();
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync())
                    return r.GetInt32(r.GetOrdinal("id"));
            }
            return null;
        }

        private async Task<Dictionary<string, int>> CustomerIdsByCodesAsync(IDb db, IEnumerable<string> customerCodes)
        {
            var codes = customerCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (codes.Count == 0)
                return new Dictionary<string, int>(StringComparer.Ordinal);

            var paramNames = codes.Select((_, i) => $"@c{i}").ToList();
            var cmd = db.GetCommand($@"
SELECT code, id
FROM customers
WHERE code IN ({string.Join(", ", paramNames)});");
            for (int i = 0; i < codes.Count; i++)
                db.AddParameter(cmd, $"c{i}", DbTypes.Types.String).Value = codes[i];

            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            using (DbDataReader r = await db.Execute(cmd))
            {
                while (await r.ReadAsync())
                {
                    var code = r.GetString(r.GetOrdinal("code"));
                    var id = r.GetInt32(r.GetOrdinal("id"));
                    map[code] = id;
                }
            }
            return map;
        }

        private async Task<string?> GetCustomerCodeByIdAsync(IDb db, int customerId)
        {
            if (customerId <= 0) return null;
            var cmd = db.GetCommand("SELECT code FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = customerId;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync())
                    return r.IsDBNull(r.GetOrdinal("code")) ? null : r.GetString(r.GetOrdinal("code"));
            }
            return null;
        }

        private async Task<(string CustomerCode, int CustomerId, string? Error)> ResolveCustomerLinkAsync(
            IDb db,
            int customerId,
            string? customerCode)
        {
            if (!string.IsNullOrWhiteSpace(customerCode))
            {
                var trimmed = customerCode.Trim();
                var cmd = db.GetCommand("SELECT id, code FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
                db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                using (DbDataReader r = await db.Execute(cmd))
                {
                    if (!await r.ReadAsync())
                        return (string.Empty, 0, $"Unknown customer code: \"{trimmed}\"");
                    return (r.GetString(r.GetOrdinal("code")), r.GetInt32(r.GetOrdinal("id")), null);
                }
            }

            if (customerId <= 0)
                return (string.Empty, 0, "Provide customerId or customerCode");

            var byId = db.GetCommand("SELECT id, code FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
            db.AddParameter(byId, "id", DbTypes.Types.Integer).Value = customerId;
            using (DbDataReader r2 = await db.Execute(byId))
            {
                if (!await r2.ReadAsync())
                    return (string.Empty, 0, "Customer not found or has no code assigned");
                var code = r2.IsDBNull(r2.GetOrdinal("code")) ? "" : r2.GetString(r2.GetOrdinal("code"));
                if (string.IsNullOrWhiteSpace(code))
                    return (string.Empty, 0, "Customer not found or has no code assigned");
                return (code, r2.GetInt32(r2.GetOrdinal("id")), null);
            }
        }

        private async Task AttachTimelineCreatedByNamesAsync(List<InvoiceTimelineEntryDto> rows)
        {
            if (rows.Count == 0) return;
            var lookup = await ResolveUserDisplayNamesByIdAsync(rows.Select(r => (long?)r.CreatedBy));
            foreach (var row in rows)
            {
                if (lookup.TryGetValue(row.CreatedBy, out var name))
                    row.CreatedByName = name;
            }
        }

        public async Task<ApiResponse<PaginatedResponse<InvoiceResponseDto>>> GetAllInvoices(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var offset = Math.Max(0, (pageNumber - 1) * pageSize);
                int total = 0;
                var items = new List<Invoice>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var countCmd = db.GetCommand("SELECT COUNT(*)::int AS total FROM invoices WHERE is_active=true;");
                    using (DbDataReader r = await db.Execute(countCmd))
                    {
                        if (await r.ReadAsync())
                            total = r.GetInt32(r.GetOrdinal("total"));
                    }

                    var cmd = db.GetCommand(@"
SELECT *
FROM invoices
ORDER BY id DESC
LIMIT @limit OFFSET @offset;");
                    db.AddParameter(cmd, "limit", DbTypes.Types.Integer).Value = pageSize;
                    db.AddParameter(cmd, "offset", DbTypes.Types.Integer).Value = offset;
                    using (DbDataReader r2 = await db.Execute(cmd))
                    {
                        while (await r2.ReadAsync())
                            items.Add(ReadInvoice(r2));
                    }

                    var custMap = await CustomerIdsByCodesAsync(db, items.Select(x => x.CustomerCode));
                    foreach (var inv in items)
                    {
                        if (inv.CustomerId > 0)
                            inv.Customer = new Customer { Id = inv.CustomerId };
                        else if (custMap.TryGetValue(inv.CustomerCode, out var cid))
                            inv.Customer = new Customer { Id = cid };
                    }
                }
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(items.Select(i => i.CreatedBy));
                var invDtos = items.Select(i => MapInvoice(i, createdByLookup)).ToList();
                await EnrichInvoicesAsync(invDtos);
                return new ApiResponse<PaginatedResponse<InvoiceResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<InvoiceResponseDto>
                    {
                        Items = invDtos,
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceResponseDto>>> GetAll()
        {
            try
            {
                var list = new List<Invoice>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM invoices WHERE is_active=true ORDER BY id DESC;");
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadInvoice(r));
                    }
                    var custMap = await CustomerIdsByCodesAsync(db, list.Select(x => x.CustomerCode));
                    foreach (var inv in list)
                    {
                        if (inv.CustomerId > 0)
                            inv.Customer = new Customer { Id = inv.CustomerId };
                        else if (custMap.TryGetValue(inv.CustomerCode, out var cid))
                            inv.Customer = new Customer { Id = cid };
                    }
                }
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(list.Select(i => i.CreatedBy));
                var invDtos = list.Select(i => MapInvoice(i, createdByLookup)).ToList();
                await EnrichInvoicesAsync(invDtos);
                return new ApiResponse<List<InvoiceResponseDto>> { Success = true, Data = invDtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GetById(int id)
        {
            try
            {
                Invoice? i = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM invoices WHERE id=@id AND is_active=true LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            i = ReadInvoice(r);
                    }
                    if (i != null)
                    {
                        var cid = i.CustomerId > 0 ? i.CustomerId : await GetCustomerIdByCodeAsync(db, i.CustomerCode) ?? 0;
                        i.Customer = new Customer { Id = cid };
                    }
                }
                if (i == null) return new ApiResponse<InvoiceResponseDto> { Success = false, Message = "Invoice not found" };
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(new[] { i.CreatedBy });
                var one = MapInvoice(i, createdByLookup);
                await EnrichInvoicesAsync(new List<InvoiceResponseDto> { one });
                return new ApiResponse<InvoiceResponseDto> { Success = true, Data = one };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvoiceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceResponseDto>>> GetInvoicesByCustomer(int customerId)
        {
            try
            {
                string? cc;
                using (IDb db0 = await dbprovider.GetDb())
                {
                    await db0.Connect();
                    cc = await GetCustomerCodeByIdAsync(db0, customerId);
                }
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<InvoiceResponseDto>> { Success = true, Data = new List<InvoiceResponseDto>() };
                var list = new List<Invoice>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM invoices
WHERE customer_id=@cid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadInvoice(r));
                    }
                    foreach (var inv in list)
                        inv.Customer = new Customer { Id = inv.CustomerId > 0 ? inv.CustomerId : customerId };
                }
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(list.Select(i => i.CreatedBy));
                var invDtos = list.Select(i => MapInvoice(i, createdByLookup)).ToList();
                await EnrichInvoicesAsync(invDtos);
                return new ApiResponse<List<InvoiceResponseDto>> { Success = true, Data = invDtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceResponseDto>>> GetInvoicesByCustomerCode(string customerCode)
        {
            try
            {
                int cid;
                string? err;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var res = await ResolveCustomerLinkAsync(db, 0, customerCode);
                    cid = res.CustomerId;
                    err = res.Error;
                }
                if (err != null)
                    return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = err };
                return await GetInvoicesByCustomer(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceResponseDto>>> GetByStaffId(int staffId)
        {
            try
            {
                var list = new List<Invoice>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM invoices
WHERE staff_id=@sid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "sid", DbTypes.Types.Integer).Value = staffId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadInvoice(r));
                    }
                    foreach (var inv in list)
                    {
                        var cid = inv.CustomerId > 0 ? inv.CustomerId : await GetCustomerIdByCodeAsync(db, inv.CustomerCode) ?? 0;
                        inv.Customer = new Customer { Id = cid };
                    }
                }
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(list.Select(i => i.CreatedBy));
                var invDtos = list.Select(i => MapInvoice(i, createdByLookup)).ToList();
                await EnrichInvoicesAsync(invDtos);
                return new ApiResponse<List<InvoiceResponseDto>> { Success = true, Data = invDtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceResponseDto>> CreateInvoice(CreateInvoiceDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        var (custCode, custId, cErr) = await ResolveCustomerLinkAsync(db, dto.CustomerId, dto.CustomerCode);
                        if (cErr != null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<InvoiceResponseDto> { Success = false, Message = cErr };
                        }

                        int? staffId = dto.StaffId;
                        if (!staffId.HasValue && dto.ServiceId > 0)
                        {
                            var svcCmd = db.GetCommand("SELECT created_by FROM services WHERE id=@id LIMIT 1;");
                            db.AddParameter(svcCmd, "id", DbTypes.Types.Integer).Value = dto.ServiceId;
                            long? createdBy = null;
                            using (DbDataReader rr = await db.Execute(svcCmd))
                            {
                                if (await rr.ReadAsync())
                                    createdBy = rr.IsDBNull(rr.GetOrdinal("created_by")) ? null : rr.GetInt64(rr.GetOrdinal("created_by"));
                            }
                            staffId = await ResolveStaffIdFromServiceCreatedByAsync(createdBy);
                        }

                        var cmd = db.GetCommand(@"
INSERT INTO invoices (
    invoice_number,
    customer_id,
    customer_code,
    service_id,
    staff_id,
    payment_mode_id,
    payment_status_id,
    receivable,
    received,
    subscription_start_at,
    subscription_end_at,
    is_active,
    created_at,
    created_by,
    paid_at,
    paid_by,
    modified_at,
    modified_by
)
VALUES (
    @invoice_number,
    @customer_id,
    @customer_code,
    @service_id,
    @staff_id,
    @payment_mode_id,
    @payment_status_id,
    @receivable,
    @received,
    @subscription_start_at,
    @subscription_end_at,
    true,
    @created_at,
    @created_by,
    NULL,
    NULL,
    @modified_at,
    @modified_by
)
RETURNING id;");
                        db.AddParameter(cmd, "invoice_number", DbTypes.Types.String).Value = dto.InvoiceNumber;
                        db.AddParameter(cmd, "customer_id", DbTypes.Types.Integer).Value = custId;
                        db.AddParameter(cmd, "customer_code", DbTypes.Types.String).Value = custCode;
                        db.AddParameter(cmd, "service_id", DbTypes.Types.Integer).Value = dto.ServiceId;
                        db.AddParameter(cmd, "staff_id", DbTypes.Types.Integer).Value = staffId.HasValue ? staffId.Value : DBNull.Value;
                        db.AddParameter(cmd, "payment_mode_id", DbTypes.Types.Integer).Value = dto.PaymentModeId;
                        db.AddParameter(cmd, "payment_status_id", DbTypes.Types.Integer).Value = dto.PaymentStatusId;
                        db.AddParameter(cmd, "receivable", DbTypes.Types.Decimal).Value = dto.Receivable;
                        db.AddParameter(cmd, "received", DbTypes.Types.Decimal).Value = dto.Received;
                        db.AddParameter(cmd, "subscription_start_at", DbTypes.Types.DateTime).Value = dto.SubscriptionStartAt;
                        db.AddParameter(cmd, "subscription_end_at", DbTypes.Types.DateTime).Value = dto.SubscriptionEndAt;
                        db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                        db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                        int newId = 0;
                        using (DbDataReader rr2 = await db.Execute(cmd))
                        {
                            if (await rr2.ReadAsync())
                                newId = rr2.GetInt32(rr2.GetOrdinal("id"));
                        }

                        await SyncCustomerSalesFlagsFromInvoiceAsync(custCode, dto.InvoiceNumber, dto.Received);
                        await db.CommitTransaction();
                        return await GetById(newId);
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvoiceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceResponseDto>> UpdateInvoice(int id, UpdateInvoiceDto dto)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        Invoice? existing = null;
                        var load = db.GetCommand("SELECT * FROM invoices WHERE id=@id AND is_active=true LIMIT 1;");
                        db.AddParameter(load, "id", DbTypes.Types.Integer).Value = id;
                        using (DbDataReader r = await db.Execute(load))
                        {
                            if (await r.ReadAsync())
                                existing = ReadInvoice(r);
                        }
                        if (existing == null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<InvoiceResponseDto> { Success = false, Message = "Invoice not found" };
                        }

                        if (dto.PaymentStatusId.HasValue) existing.PaymentStatusId = dto.PaymentStatusId.Value;
                        if (dto.Receivable.HasValue) existing.Receivable = dto.Receivable.Value;
                        if (dto.Received.HasValue) existing.Received = dto.Received.Value;
                        if (dto.SubscriptionStartAt.HasValue) existing.SubscriptionStartAt = dto.SubscriptionStartAt.Value;
                        if (dto.SubscriptionEndAt.HasValue) existing.SubscriptionEndAt = dto.SubscriptionEndAt.Value;
                        if (dto.IsActive.HasValue) existing.IsActive = dto.IsActive.Value;
                        if (dto.PaidAt.HasValue) existing.PaidAt = dto.PaidAt;
                        if (dto.PaidBy != null) existing.PaidBy = dto.PaidBy;
                        existing.ModifiedAt = DateTime.UtcNow;
                        existing.ModifiedBy = AuditUserIds.System;

                        var upd = db.GetCommand(@"
UPDATE invoices SET
    payment_status_id=@payment_status_id,
    receivable=@receivable,
    received=@received,
    subscription_start_at=@subscription_start_at,
    subscription_end_at=@subscription_end_at,
    is_active=@is_active,
    paid_at=@paid_at,
    paid_by=@paid_by,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                        db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = existing.Id;
                        db.AddParameter(upd, "payment_status_id", DbTypes.Types.Integer).Value = existing.PaymentStatusId;
                        db.AddParameter(upd, "receivable", DbTypes.Types.Decimal).Value = existing.Receivable;
                        db.AddParameter(upd, "received", DbTypes.Types.Decimal).Value = existing.Received;
                        db.AddParameter(upd, "subscription_start_at", DbTypes.Types.DateTime).Value = existing.SubscriptionStartAt;
                        db.AddParameter(upd, "subscription_end_at", DbTypes.Types.DateTime).Value = existing.SubscriptionEndAt;
                        db.AddParameter(upd, "is_active", DbTypes.Types.Boolean).Value = existing.IsActive;
                        db.AddParameter(upd, "paid_at", DbTypes.Types.DateTime).Value = existing.PaidAt.HasValue ? existing.PaidAt.Value : DBNull.Value;
                        db.AddParameter(upd, "paid_by", DbTypes.Types.String).Value = existing.PaidBy ?? (object)DBNull.Value;
                        db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = existing.ModifiedAt;
                        db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = existing.ModifiedBy ?? (object)DBNull.Value;
                        await db.ExecuteNonQuery(upd);

                        await SyncCustomerSalesFlagsFromInvoiceAsync(existing.CustomerCode, existing.InvoiceNumber, existing.Received);
                        await db.CommitTransaction();
                        return await GetById(existing.Id);
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvoiceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteInvoice(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
UPDATE invoices
SET is_active=false,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id
RETURNING id;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = DateTime.UtcNow;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (!await r.ReadAsync())
                            return new ApiResponse<bool> { Success = false, Message = "Invoice not found" };
                    }
                    return new ApiResponse<bool> { Success = true, Data = true };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceTimelineEntryDto>>> GetTimeline(int invoiceId)
        {
            try
            {
                var rows = new List<InvoiceTimelineEntryDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT id, invoice_id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by
FROM invoice_timelines
WHERE invoice_id=@invoice_id
ORDER BY id DESC;");
                    db.AddParameter(cmd, "invoice_id", DbTypes.Types.Integer).Value = invoiceId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            rows.Add(new InvoiceTimelineEntryDto
                            {
                                Id = r.GetInt32(r.GetOrdinal("id")),
                                InvoiceId = r.GetInt32(r.GetOrdinal("invoice_id")),
                                Type = r.GetInt32(r.GetOrdinal("type")),
                                Notes = r.GetString(r.GetOrdinal("notes")),
                                FileId = r.IsDBNull(r.GetOrdinal("file_id")) ? null : r.GetInt32(r.GetOrdinal("file_id")),
                                FileName = r.IsDBNull(r.GetOrdinal("file_name")) ? null : r.GetString(r.GetOrdinal("file_name")),
                                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                                CreatedBy = r.GetInt64(r.GetOrdinal("created_by")),
                                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
                            });
                        }
                    }
                }
                await AttachTimelineCreatedByNamesAsync(rows);
                return new ApiResponse<List<InvoiceTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceTimelineEntryDto>> AddTimelineEntry(int invoiceId, AddTimelineEntryDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                int newId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    // Ensure invoice exists.
                    var chk = db.GetCommand("SELECT 1 FROM invoices WHERE id=@id AND is_active=true LIMIT 1;");
                    db.AddParameter(chk, "id", DbTypes.Types.Integer).Value = invoiceId;
                    using (DbDataReader r0 = await db.Execute(chk))
                    {
                        if (!await r0.ReadAsync())
                            return new ApiResponse<InvoiceTimelineEntryDto> { Success = false, Message = "Invoice not found" };
                    }

                    var cmd = db.GetCommand(@"
INSERT INTO invoice_timelines (
    invoice_id, type, notes, file_id, file_name,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @invoice_id, @type, @notes, @file_id, @file_name,
    true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                    db.AddParameter(cmd, "invoice_id", DbTypes.Types.Integer).Value = invoiceId;
                    db.AddParameter(cmd, "type", DbTypes.Types.Integer).Value = dto.Type;
                    db.AddParameter(cmd, "notes", DbTypes.Types.String).Value = dto.Notes;
                    db.AddParameter(cmd, "file_id", DbTypes.Types.Integer).Value = dto.FileId.HasValue ? dto.FileId.Value : DBNull.Value;
                    db.AddParameter(cmd, "file_name", DbTypes.Types.String).Value = dto.FileName ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            newId = r.GetInt32(r.GetOrdinal("id"));
                    }
                }

                return new ApiResponse<InvoiceTimelineEntryDto>
                {
                    Success = true,
                    Data = new InvoiceTimelineEntryDto
                    {
                        Id = newId,
                        InvoiceId = invoiceId,
                        Type = dto.Type,
                        Notes = dto.Notes,
                        FileId = dto.FileId,
                        FileName = dto.FileName,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = AuditUserIds.System,
                        ModifiedAt = now,
                        ModifiedBy = AuditUserIds.System
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvoiceTimelineEntryDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
