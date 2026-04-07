using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>InvestmentService.ts</c>.</summary>
    public interface IInvestmentService
    {
        Task<ApiResponse<List<InvestmentResponseDto>>> GetAll();
        Task<ApiResponse<InvestmentResponseDto>> GetInvestmentById(int id);
        Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerId(int customerId);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerCode(string customerCode);
        Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomerCode(string customerCode, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<decimal>> GetTotalInvestmentByCustomerCode(string customerCode);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByStaffId(int staffId);
        Task<ApiResponse<InvestmentResponseDto>> CreateInvestment(CreateInvestmentDto dto);
        Task<ApiResponse<InvestmentResponseDto>> UpdateInvestment(int id, UpdateInvestmentDto dto);
        Task<ApiResponse<bool>> DeleteInvestment(int id);
        Task<ApiResponse<decimal>> GetTotalInvestmentByCustomer(int customerId);
        Task<ApiResponse<List<InvestmentTimelineEntryDto>>> GetTimeline(int investmentId);
        Task<ApiResponse<InvestmentTimelineEntryDto>> AddTimelineEntry(int investmentId, AddTimelineEntryDto dto);
        Task<ApiResponse<InvestmentResponseDto>> ClaimInvestment(ClaimInvestmentDto dto);
        Task<ApiResponse<List<InvestmentClaimSummaryDto>>> GetClaimSummary(DateTime startUtc, DateTime endUtc, long? userId);
        Task<ApiResponse<List<InvestmentClaimRowDto>>> GetClaimRows(DateTime startUtc, DateTime endUtc, long? userId);
    }

    public class InvestmentService : IInvestmentService
    {
        IDbProvider dbprovider;

        public InvestmentService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        private async Task EnrichInvestmentsAsync(List<InvestmentResponseDto> rows)
        {
            if (rows.Count == 0) return;
            var custIds = rows.Select(r => r.CustomerId).Where(id => id > 0).Distinct().ToList();
            if (custIds.Count == 0) return;
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var custParamNames = custIds.Select((_, i) => $"@c{i}").ToList();
                var cmd = db.GetCommand($@"
SELECT id, code
FROM customers
WHERE id IN ({string.Join(", ", custParamNames)});");
                for (int i = 0; i < custIds.Count; i++)
                    db.AddParameter(cmd, $"c{i}", DbTypes.Types.Integer).Value = custIds[i];

                var custMap = new Dictionary<int, string?>();
                using (DbDataReader r = await db.Execute(cmd))
                {
                    while (await r.ReadAsync())
                        custMap[r.GetInt32(r.GetOrdinal("id"))] = r.IsDBNull(r.GetOrdinal("code")) ? null : r.GetString(r.GetOrdinal("code"));
                }

                var locIds = rows.Where(r => r.LocationId > 0).Select(r => r.LocationId).Distinct().ToList();
                Dictionary<int, string> locMap = new();
                if (locIds.Count > 0)
                {
                    var locParamNames = locIds.Select((_, i) => $"@l{i}").ToList();
                    var cmd2 = db.GetCommand($@"
SELECT id, code
FROM locations
WHERE id IN ({string.Join(", ", locParamNames)});");
                    for (int i = 0; i < locIds.Count; i++)
                        db.AddParameter(cmd2, $"l{i}", DbTypes.Types.Integer).Value = locIds[i];
                    using (DbDataReader r2 = await db.Execute(cmd2))
                    {
                        while (await r2.ReadAsync())
                            locMap[r2.GetInt32(r2.GetOrdinal("id"))] = r2.GetString(r2.GetOrdinal("code"));
                    }
                }

                foreach (var row in rows)
                {
                    row.CustomerCode = custMap.GetValueOrDefault(row.CustomerId);
                    if (locMap.TryGetValue(row.LocationId, out var lc))
                        row.LocationCode = lc;
                }
            }
        }

        private static InvestmentResponseDto Map(Investment i) => new()
        {
            Id = i.Id,
            CustomerId = i.Customer?.Id ?? i.CustomerId,
            LocationId = i.LocationId,
            Amount = i.Amount,
            ClaimedAmount = i.ClaimedAmount,
            RemainingAmount = i.RemainingAmount,
            ClaimedFully = i.ClaimedFully,
            ClaimedAt = i.ClaimedAt,
            ClaimedBy = i.ClaimedBy,
            ClaimNotes = i.ClaimNotes,
            NeedsClaim = i.NeedsClaim,
            InvestmentTypeId = i.InvestmentTypeId,
            StaffId = i.StaffId,
            Notes = i.Notes,
            IsActive = i.IsActive,
            CreatedAt = i.CreatedAt,
            CreatedBy = i.CreatedBy,
            ModifiedAt = i.ModifiedAt
        };

        private static Investment ReadInvestment(DbDataReader r)
        {
            return new Investment
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                CustomerId = r.GetInt32(r.GetOrdinal("customer_id")),
                CustomerCode = r.GetString(r.GetOrdinal("customer_code")),
                LocationId = r.GetInt32(r.GetOrdinal("location_id")),
                Amount = r.GetDecimal(r.GetOrdinal("amount")),
                ClaimedAmount = r.GetDecimal(r.GetOrdinal("claimed_amount")),
                RemainingAmount = r.GetDecimal(r.GetOrdinal("remaining_amount")),
                ClaimedFully = r.GetBoolean(r.GetOrdinal("claimed_fully")),
                ClaimedAt = r.IsDBNull(r.GetOrdinal("claimed_at")) ? null : r.GetDateTime(r.GetOrdinal("claimed_at")),
                ClaimedBy = r.IsDBNull(r.GetOrdinal("claimed_by")) ? null : r.GetInt64(r.GetOrdinal("claimed_by")),
                ClaimNotes = r.IsDBNull(r.GetOrdinal("claim_notes")) ? null : r.GetString(r.GetOrdinal("claim_notes")),
                NeedsClaim = r.GetBoolean(r.GetOrdinal("needs_claim")),
                InvestmentTypeId = r.GetInt32(r.GetOrdinal("investment_type_id")),
                StaffId = r.IsDBNull(r.GetOrdinal("staff_id")) ? null : r.GetInt32(r.GetOrdinal("staff_id")),
                Notes = r.GetString(r.GetOrdinal("notes")),
                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                CreatedBy = r.IsDBNull(r.GetOrdinal("created_by")) ? null : r.GetInt64(r.GetOrdinal("created_by")),
                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
            };
        }

        private static InvestmentTimelineEntryDto MapTimeline(DbDataReader r)
        {
            return new InvestmentTimelineEntryDto
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                InvestmentId = r.GetInt32(r.GetOrdinal("investment_id")),
                Type = r.GetInt32(r.GetOrdinal("type")),
                Notes = r.GetString(r.GetOrdinal("notes")),
                FileId = r.IsDBNull(r.GetOrdinal("file_id")) ? null : r.GetInt32(r.GetOrdinal("file_id")),
                FileName = r.IsDBNull(r.GetOrdinal("file_name")) ? null : r.GetString(r.GetOrdinal("file_name")),
                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                CreatedBy = r.GetInt64(r.GetOrdinal("created_by")),
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

        private async Task<(string CustomerCode, int CustomerId, string? Error)> ResolveCustomerLinkAsync(IDb db, int customerId, string? customerCode)
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

        private async Task<(int LocationId, string? Error)> ResolveRequiredLocationIdAsync(
            IDb db,
            string customerCode,
            int locationId,
            string? locationCode)
        {
            if (string.IsNullOrWhiteSpace(customerCode))
                return (0, "Customer code is required to resolve location");

            var cc = customerCode.Trim();
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                var trimmed = locationCode.Trim();
                var cmd = db.GetCommand(@"
SELECT id
FROM locations
WHERE customer_code=@cc AND code=@code
LIMIT 1;");
                db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                using (DbDataReader r = await db.Execute(cmd))
                {
                    if (!await r.ReadAsync())
                        return (0, $"Unknown location code \"{trimmed}\" for this customer");
                    return (r.GetInt32(r.GetOrdinal("id")), null);
                }
            }

            if (locationId <= 0)
                return (0, "Provide locationId or locationCode");
            return (locationId, null);
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetAll()
        {
            try
            {
                var list = new List<Investment>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM investments ORDER BY id DESC;");
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadInvestment(r));
                    }
                    // Attach customer ids for DTO mapping
                    var codes = list.Select(x => x.CustomerCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                    if (codes.Count > 0)
                    {
                        var paramNames = codes.Select((_, i) => $"@c{i}").ToList();
                        var cmd2 = db.GetCommand($@"SELECT code, id FROM customers WHERE code IN ({string.Join(", ", paramNames)});");
                        for (int i = 0; i < codes.Count; i++)
                            db.AddParameter(cmd2, $"c{i}", DbTypes.Types.String).Value = codes[i];
                        var map = new Dictionary<string, int>(StringComparer.Ordinal);
                        using (DbDataReader r2 = await db.Execute(cmd2))
                        {
                            while (await r2.ReadAsync())
                                map[r2.GetString(r2.GetOrdinal("code"))] = r2.GetInt32(r2.GetOrdinal("id"));
                        }
                        foreach (var it in list)
                        {
                            if (map.TryGetValue(it.CustomerCode, out var cid))
                            {
                                it.CustomerId = cid;
                                it.Customer = new Customer { Id = cid };
                            }
                        }
                    }
                }
                var dtos = list.Select(Map).ToList();
                await EnrichInvestmentsAsync(dtos);
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> GetInvestmentById(int id)
        {
            try
            {
                Investment? inv = null;
                int customerId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM investments WHERE id=@id LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            inv = ReadInvestment(r);
                    }
                    if (inv != null)
                        customerId = await GetCustomerIdByCodeAsync(db, inv.CustomerCode) ?? 0;
                }
                if (inv == null) return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };
                inv.Customer = new Customer { Id = customerId };
                var one = Map(inv);
                await EnrichInvestmentsAsync(new List<InvestmentResponseDto> { one });
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = one };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10)
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
                    return new ApiResponse<PaginatedResponse<InvestmentResponseDto>>
                    {
                        Success = true,
                        Data = new PaginatedResponse<InvestmentResponseDto>
                        {
                            Items = new List<InvestmentResponseDto>(),
                            Total = 0,
                            PageNumber = pageNumber,
                            PageSize = pageSize
                        }
                    };
                var offset = Math.Max(0, (pageNumber - 1) * pageSize);
                int total = 0;
                var items = new List<Investment>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var countCmd = db.GetCommand("SELECT COUNT(*)::int AS total FROM investments WHERE customer_code=@cc;");
                    db.AddParameter(countCmd, "cc", DbTypes.Types.String).Value = cc;
                    using (DbDataReader r = await db.Execute(countCmd))
                    {
                        if (await r.ReadAsync()) total = r.GetInt32(r.GetOrdinal("total"));
                    }
                    var cmd = db.GetCommand(@"
SELECT *
FROM investments
WHERE customer_code=@cc
ORDER BY id DESC
LIMIT @limit OFFSET @offset;");
                    db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                    db.AddParameter(cmd, "limit", DbTypes.Types.Integer).Value = pageSize;
                    db.AddParameter(cmd, "offset", DbTypes.Types.Integer).Value = offset;
                    using (DbDataReader r2 = await db.Execute(cmd))
                    {
                        while (await r2.ReadAsync())
                            items.Add(ReadInvestment(r2));
                    }
                }
                foreach (var it in items) it.Customer = new Customer { Id = customerId };
                var dtos = items.Select(Map).ToList();
                await EnrichInvestmentsAsync(dtos);
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<InvestmentResponseDto>
                    {
                        Items = dtos,
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerId(int customerId)
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
                    return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = new List<InvestmentResponseDto>() };
                var list = new List<Investment>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM investments
WHERE customer_code=@cc
ORDER BY id DESC;");
                    db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadInvestment(r));
                    }
                }
                foreach (var it in list)
                {
                    it.CustomerId = customerId;
                    it.Customer = new Customer { Id = customerId };
                }
                var dtos = list.Select(Map).ToList();
                await EnrichInvestmentsAsync(dtos);
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerCode(string customerCode)
        {
            try
            {
                int cid = 0;
                string? err = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var res = await ResolveCustomerLinkAsync(db, 0, customerCode);
                    cid = res.CustomerId;
                    err = res.Error;
                }
                if (err != null)
                    return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = err };
                return await GetByCustomerId(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomerCode(
            string customerCode,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                int cid = 0;
                string? err = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var res = await ResolveCustomerLinkAsync(db, 0, customerCode);
                    cid = res.CustomerId;
                    err = res.Error;
                }
                if (err != null)
                    return new ApiResponse<PaginatedResponse<InvestmentResponseDto>> { Success = false, Message = err };
                return await GetInvestmentsByCustomer(cid, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByStaffId(int staffId)
        {
            try
            {
                var list = new List<Investment>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM investments
WHERE staff_id=@sid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "sid", DbTypes.Types.Integer).Value = staffId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadInvestment(r));
                    }
                    var codes = list.Select(x => x.CustomerCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                    if (codes.Count > 0)
                    {
                        var paramNames = codes.Select((_, i) => $"@c{i}").ToList();
                        var cmd2 = db.GetCommand($@"SELECT code, id FROM customers WHERE code IN ({string.Join(", ", paramNames)});");
                        for (int i = 0; i < codes.Count; i++)
                            db.AddParameter(cmd2, $"c{i}", DbTypes.Types.String).Value = codes[i];
                        var map = new Dictionary<string, int>(StringComparer.Ordinal);
                        using (DbDataReader r2 = await db.Execute(cmd2))
                        {
                            while (await r2.ReadAsync())
                                map[r2.GetString(r2.GetOrdinal("code"))] = r2.GetInt32(r2.GetOrdinal("id"));
                        }
                        foreach (var it in list)
                        {
                            if (map.TryGetValue(it.CustomerCode, out var cid))
                            {
                                it.CustomerId = cid;
                                it.Customer = new Customer { Id = cid };
                            }
                        }
                    }
                }
                var dtos = list.Select(Map).ToList();
                await EnrichInvestmentsAsync(dtos);
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> CreateInvestment(CreateInvestmentDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var (custCode, custId, cErr) = await ResolveCustomerLinkAsync(db, dto.CustomerId, dto.CustomerCode);
                    if (cErr != null)
                        return new ApiResponse<InvestmentResponseDto> { Success = false, Message = cErr };
                    var (locationId, lErr) = await ResolveRequiredLocationIdAsync(db, custCode, dto.LocationId, dto.LocationCode);
                    if (lErr != null)
                        return new ApiResponse<InvestmentResponseDto> { Success = false, Message = lErr };

                    var remaining = dto.Amount < 0 ? 0 : dto.Amount;
                    var cmd = db.GetCommand(@"
INSERT INTO investments (
    customer_id, customer_code, location_id, amount,
    claimed_amount, remaining_amount, claimed_fully,
    claimed_at, claimed_by, claim_notes,
    needs_claim, investment_type_id, staff_id, notes,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @customer_id, @customer_code, @location_id, @amount,
    0, @remaining_amount, false,
    NULL, NULL, NULL,
    @needs_claim, @investment_type_id, @staff_id, @notes,
    true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                    db.AddParameter(cmd, "customer_id", DbTypes.Types.Integer).Value = custId;
                    db.AddParameter(cmd, "customer_code", DbTypes.Types.String).Value = custCode;
                    db.AddParameter(cmd, "location_id", DbTypes.Types.Integer).Value = locationId;
                    db.AddParameter(cmd, "amount", DbTypes.Types.Decimal).Value = dto.Amount;
                    db.AddParameter(cmd, "remaining_amount", DbTypes.Types.Decimal).Value = remaining;
                    db.AddParameter(cmd, "needs_claim", DbTypes.Types.Boolean).Value = dto.NeedsClaim != false;
                    db.AddParameter(cmd, "investment_type_id", DbTypes.Types.Integer).Value = dto.InvestmentTypeId;
                    db.AddParameter(cmd, "staff_id", DbTypes.Types.Integer).Value = dto.StaffId.HasValue ? dto.StaffId.Value : DBNull.Value;
                    db.AddParameter(cmd, "notes", DbTypes.Types.String).Value = dto.Notes;
                    db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    int newId = 0;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            newId = r.GetInt32(r.GetOrdinal("id"));
                    }

                    var inv = new Investment
                    {
                        Id = newId,
                        CustomerId = custId,
                        CustomerCode = custCode,
                        LocationId = locationId,
                        Amount = dto.Amount,
                        ClaimedAmount = 0,
                        RemainingAmount = remaining,
                        ClaimedFully = false,
                        NeedsClaim = dto.NeedsClaim != false,
                        InvestmentTypeId = dto.InvestmentTypeId,
                        StaffId = dto.StaffId,
                        Notes = dto.Notes,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = AuditUserIds.System,
                        ModifiedAt = now,
                        ModifiedBy = AuditUserIds.System,
                        Customer = new Customer { Id = custId }
                    };
                    var created = Map(inv);
                    await EnrichInvestmentsAsync(new List<InvestmentResponseDto> { created });
                    return new ApiResponse<InvestmentResponseDto> { Success = true, Data = created };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> UpdateInvestment(int id, UpdateInvestmentDto dto)
        {
            try
            {
                Investment? inv = null;
                int customerId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var load = db.GetCommand("SELECT * FROM investments WHERE id=@id LIMIT 1;");
                    db.AddParameter(load, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r0 = await db.Execute(load))
                    {
                        if (await r0.ReadAsync())
                            inv = ReadInvestment(r0);
                    }
                    if (inv == null) return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };

                    if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                    {
                        var res = await ResolveCustomerLinkAsync(db, 0, dto.CustomerCode);
                        if (res.Error != null)
                            return new ApiResponse<InvestmentResponseDto> { Success = false, Message = res.Error };
                        inv.CustomerCode = res.CustomerCode;
                        customerId = res.CustomerId;
                    }
                    else if (dto.CustomerId.HasValue)
                    {
                        var res = await ResolveCustomerLinkAsync(db, dto.CustomerId.Value, null);
                        if (res.Error != null)
                            return new ApiResponse<InvestmentResponseDto> { Success = false, Message = res.Error };
                        inv.CustomerCode = res.CustomerCode;
                        customerId = res.CustomerId;
                    }
                    else
                    {
                        customerId = await GetCustomerIdByCodeAsync(db, inv.CustomerCode) ?? 0;
                    }

                    inv.CustomerId = customerId > 0 ? customerId : inv.CustomerId;

                    if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                    {
                        var (lid, lErr) = await ResolveRequiredLocationIdAsync(db, inv.CustomerCode, 0, dto.LocationCode);
                        if (lErr != null)
                            return new ApiResponse<InvestmentResponseDto> { Success = false, Message = lErr };
                        inv.LocationId = lid;
                    }
                    else if (dto.LocationId.HasValue)
                        inv.LocationId = dto.LocationId.Value;
                    if (dto.InvestmentTypeId.HasValue) inv.InvestmentTypeId = dto.InvestmentTypeId.Value;
                    if (dto.Amount.HasValue)
                    {
                        inv.Amount = dto.Amount.Value;
                        if (inv.ClaimedAmount < 0) inv.ClaimedAmount = 0;
                        if (inv.ClaimedAmount > inv.Amount) inv.ClaimedAmount = inv.Amount;
                        inv.RemainingAmount = Math.Max(0, inv.Amount - inv.ClaimedAmount);
                        inv.ClaimedFully = inv.RemainingAmount == 0 && inv.Amount > 0;
                    }
                    if (dto.StaffIdCleared == true)
                        inv.StaffId = null;
                    else if (dto.StaffId.HasValue)
                        inv.StaffId = dto.StaffId.Value;
                    if (dto.Notes != null) inv.Notes = dto.Notes;
                    if (dto.IsActive.HasValue) inv.IsActive = dto.IsActive.Value;
                    if (dto.NeedsClaim.HasValue) inv.NeedsClaim = dto.NeedsClaim.Value;
                    inv.ModifiedAt = DateTime.UtcNow;
                    inv.ModifiedBy = AuditUserIds.System;

                    var upd = db.GetCommand(@"
UPDATE investments SET
    customer_id=@customer_id,
    customer_code=@customer_code,
    location_id=@location_id,
    amount=@amount,
    claimed_amount=@claimed_amount,
    remaining_amount=@remaining_amount,
    claimed_fully=@claimed_fully,
    claimed_at=@claimed_at,
    claimed_by=@claimed_by,
    claim_notes=@claim_notes,
    needs_claim=@needs_claim,
    investment_type_id=@investment_type_id,
    staff_id=@staff_id,
    notes=@notes,
    is_active=@is_active,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                    db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = inv.Id;
                    db.AddParameter(upd, "customer_id", DbTypes.Types.Integer).Value = inv.CustomerId;
                    db.AddParameter(upd, "customer_code", DbTypes.Types.String).Value = inv.CustomerCode;
                    db.AddParameter(upd, "location_id", DbTypes.Types.Integer).Value = inv.LocationId;
                    db.AddParameter(upd, "amount", DbTypes.Types.Decimal).Value = inv.Amount;
                    db.AddParameter(upd, "claimed_amount", DbTypes.Types.Decimal).Value = inv.ClaimedAmount;
                    db.AddParameter(upd, "remaining_amount", DbTypes.Types.Decimal).Value = inv.RemainingAmount;
                    db.AddParameter(upd, "claimed_fully", DbTypes.Types.Boolean).Value = inv.ClaimedFully;
                    db.AddParameter(upd, "claimed_at", DbTypes.Types.DateTime).Value = inv.ClaimedAt.HasValue ? inv.ClaimedAt.Value : DBNull.Value;
                    db.AddParameter(upd, "claimed_by", DbTypes.Types.Long).Value = inv.ClaimedBy.HasValue ? inv.ClaimedBy.Value : DBNull.Value;
                    db.AddParameter(upd, "claim_notes", DbTypes.Types.String).Value = inv.ClaimNotes ?? (object)DBNull.Value;
                    db.AddParameter(upd, "needs_claim", DbTypes.Types.Boolean).Value = inv.NeedsClaim;
                    db.AddParameter(upd, "investment_type_id", DbTypes.Types.Integer).Value = inv.InvestmentTypeId;
                    db.AddParameter(upd, "staff_id", DbTypes.Types.Integer).Value = inv.StaffId.HasValue ? inv.StaffId.Value : DBNull.Value;
                    db.AddParameter(upd, "notes", DbTypes.Types.String).Value = inv.Notes;
                    db.AddParameter(upd, "is_active", DbTypes.Types.Boolean).Value = inv.IsActive;
                    db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = inv.ModifiedAt;
                    db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = inv.ModifiedBy ?? (object)DBNull.Value;
                    await db.ExecuteNonQuery(upd);
                }

                inv!.Customer = new Customer { Id = inv.CustomerId };
                var updated = Map(inv);
                await EnrichInvestmentsAsync(new List<InvestmentResponseDto> { updated });
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = updated };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteInvestment(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
UPDATE investments
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
                            return new ApiResponse<bool> { Success = false, Message = "Investment not found" };
                    }
                    return new ApiResponse<bool> { Success = true, Data = true };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<decimal>> GetTotalInvestmentByCustomer(int customerId)
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
                    return new ApiResponse<decimal> { Success = true, Data = 0 };
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT COALESCE(SUM(amount),0)::numeric AS total FROM investments WHERE customer_code=@cc;");
                    db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            return new ApiResponse<decimal> { Success = true, Data = r.GetDecimal(r.GetOrdinal("total")) };
                    }
                }
                return new ApiResponse<decimal> { Success = true, Data = 0 };
            }
            catch (Exception ex)
            {
                return new ApiResponse<decimal> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<decimal>> GetTotalInvestmentByCustomerCode(string customerCode)
        {
            try
            {
                int cid = 0;
                string? err = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var res = await ResolveCustomerLinkAsync(db, 0, customerCode);
                    cid = res.CustomerId;
                    err = res.Error;
                }
                if (err != null)
                    return new ApiResponse<decimal> { Success = false, Message = err };
                return await GetTotalInvestmentByCustomer(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<decimal> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentTimelineEntryDto>>> GetTimeline(int investmentId)
        {
            try
            {
                var rows = new List<InvestmentTimelineEntryDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT id, investment_id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by
FROM investment_timelines
WHERE investment_id=@iid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "iid", DbTypes.Types.Integer).Value = investmentId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            rows.Add(MapTimeline(r));
                    }
                }
                return new ApiResponse<List<InvestmentTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentTimelineEntryDto>> AddTimelineEntry(int investmentId, AddTimelineEntryDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                int newId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var chk = db.GetCommand("SELECT 1 FROM investments WHERE id=@id LIMIT 1;");
                    db.AddParameter(chk, "id", DbTypes.Types.Integer).Value = investmentId;
                    using (DbDataReader r0 = await db.Execute(chk))
                    {
                        if (!await r0.ReadAsync())
                            return new ApiResponse<InvestmentTimelineEntryDto> { Success = false, Message = "Investment not found" };
                    }

                    var cmd = db.GetCommand(@"
INSERT INTO investment_timelines (
    investment_id, type, notes, file_id, file_name,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @investment_id, @type, @notes, @file_id, @file_name,
    true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                    db.AddParameter(cmd, "investment_id", DbTypes.Types.Integer).Value = investmentId;
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

                return new ApiResponse<InvestmentTimelineEntryDto>
                {
                    Success = true,
                    Data = new InvestmentTimelineEntryDto
                    {
                        Id = newId,
                        InvestmentId = investmentId,
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
                return new ApiResponse<InvestmentTimelineEntryDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> ClaimInvestment(ClaimInvestmentDto dto)
        {
            if (dto.InvestmentId <= 0)
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "investmentId is required" };

            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        Investment? inv = null;
                        var load = db.GetCommand("SELECT * FROM investments WHERE id=@id LIMIT 1;");
                        db.AddParameter(load, "id", DbTypes.Types.Integer).Value = dto.InvestmentId;
                        using (DbDataReader r0 = await db.Execute(load))
                        {
                            if (await r0.ReadAsync())
                                inv = ReadInvestment(r0);
                        }
                        if (inv == null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };
                        }
                        if (inv.ClaimedFully)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Already claimed" };
                        }
                        if (!inv.NeedsClaim)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "This investment does not require a claim" };
                        }

                        var now = DateTime.UtcNow;
                        var auditUserId = dto.UserId.HasValue && dto.UserId.Value > 0 ? dto.UserId.Value : AuditUserIds.System;
                        var claimedAt = dto.ClaimedAt ?? now;

                        var amount = inv.Amount < 0 ? 0 : inv.Amount;
                        var remaining = 0m;
                        var claimAmount = Math.Max(0, amount);

                        inv.ClaimedAmount = claimAmount;
                        inv.RemainingAmount = remaining;
                        inv.ClaimedFully = amount > 0;
                        inv.ClaimedAt = claimedAt;
                        inv.ClaimedBy = auditUserId;
                        inv.ClaimNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
                        inv.ModifiedAt = now;
                        inv.ModifiedBy = auditUserId;

                        var upd = db.GetCommand(@"
UPDATE investments SET
    claimed_amount=@claimed_amount,
    remaining_amount=@remaining_amount,
    claimed_fully=@claimed_fully,
    claimed_at=@claimed_at,
    claimed_by=@claimed_by,
    claim_notes=@claim_notes,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                        db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = inv.Id;
                        db.AddParameter(upd, "claimed_amount", DbTypes.Types.Decimal).Value = inv.ClaimedAmount;
                        db.AddParameter(upd, "remaining_amount", DbTypes.Types.Decimal).Value = inv.RemainingAmount;
                        db.AddParameter(upd, "claimed_fully", DbTypes.Types.Boolean).Value = inv.ClaimedFully;
                        db.AddParameter(upd, "claimed_at", DbTypes.Types.DateTime).Value = inv.ClaimedAt.HasValue ? inv.ClaimedAt.Value : DBNull.Value;
                        db.AddParameter(upd, "claimed_by", DbTypes.Types.Long).Value = inv.ClaimedBy.HasValue ? inv.ClaimedBy.Value : DBNull.Value;
                        db.AddParameter(upd, "claim_notes", DbTypes.Types.String).Value = inv.ClaimNotes ?? (object)DBNull.Value;
                        db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = inv.ModifiedAt;
                        db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = inv.ModifiedBy ?? (object)DBNull.Value;
                        await db.ExecuteNonQuery(upd);

                        var tl = db.GetCommand(@"
INSERT INTO investment_timelines (
    investment_id, type, notes, file_id, file_name,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @investment_id, 1, @notes, NULL, NULL,
    true, @created_at, @created_by, @modified_at, @modified_by
);");
                        db.AddParameter(tl, "investment_id", DbTypes.Types.Integer).Value = inv.Id;
                        db.AddParameter(tl, "notes", DbTypes.Types.String).Value = $"Claimed fully: {claimAmount}";
                        db.AddParameter(tl, "created_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(tl, "created_by", DbTypes.Types.Long).Value = auditUserId;
                        db.AddParameter(tl, "modified_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(tl, "modified_by", DbTypes.Types.Long).Value = auditUserId;
                        await db.ExecuteNonQuery(tl);

                        await db.CommitTransaction();
                        var cid = await GetCustomerIdByCodeAsync(db, inv.CustomerCode) ?? 0;
                        inv.Customer = new Customer { Id = cid };
                        var updated = Map(inv);
                        await EnrichInvestmentsAsync(new List<InvestmentResponseDto> { updated });
                        return new ApiResponse<InvestmentResponseDto> { Success = true, Data = updated };
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
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentClaimSummaryDto>>> GetClaimSummary(DateTime startUtc, DateTime endUtc, long? userId)
        {
            try
            {
                var rows = new List<InvestmentClaimSummaryDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var where = "claimed_fully = true AND claimed_at IS NOT NULL AND claimed_at >= @start AND claimed_at <= @end";
                    if (userId.HasValue) where += " AND claimed_by = @uid";
                    var cmd = db.GetCommand($@"
SELECT claimed_by AS user_id,
       COUNT(*)::int AS count,
       COALESCE(SUM(amount),0)::numeric AS total_amount
FROM investments
WHERE {where}
GROUP BY claimed_by
ORDER BY total_amount DESC;");
                    db.AddParameter(cmd, "start", DbTypes.Types.DateTime).Value = startUtc;
                    db.AddParameter(cmd, "end", DbTypes.Types.DateTime).Value = endUtc;
                    if (userId.HasValue)
                        db.AddParameter(cmd, "uid", DbTypes.Types.Long).Value = userId.Value;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            rows.Add(new InvestmentClaimSummaryDto
                            {
                                UserId = r.IsDBNull(r.GetOrdinal("user_id")) ? null : r.GetInt64(r.GetOrdinal("user_id")),
                                Count = r.GetInt32(r.GetOrdinal("count")),
                                TotalAmount = r.GetDecimal(r.GetOrdinal("total_amount"))
                            });
                        }
                    }
                }
                return new ApiResponse<List<InvestmentClaimSummaryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentClaimSummaryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentClaimRowDto>>> GetClaimRows(DateTime startUtc, DateTime endUtc, long? userId)
        {
            try
            {
                var rows = new List<InvestmentClaimRowDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var where = "claimed_fully = true AND claimed_at IS NOT NULL AND claimed_at >= @start AND claimed_at <= @end";
                    if (userId.HasValue) where += " AND claimed_by = @uid";
                    var cmd = db.GetCommand($@"
SELECT id AS investment_id,
       customer_code,
       location_id,
       amount,
       claimed_at,
       claimed_by,
       claim_notes,
       investment_type_id,
       staff_id
FROM investments
WHERE {where}
ORDER BY id DESC;");
                    db.AddParameter(cmd, "start", DbTypes.Types.DateTime).Value = startUtc;
                    db.AddParameter(cmd, "end", DbTypes.Types.DateTime).Value = endUtc;
                    if (userId.HasValue)
                        db.AddParameter(cmd, "uid", DbTypes.Types.Long).Value = userId.Value;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            rows.Add(new InvestmentClaimRowDto
                            {
                                InvestmentId = r.GetInt32(r.GetOrdinal("investment_id")),
                                CustomerCode = r.GetString(r.GetOrdinal("customer_code")),
                                LocationId = r.GetInt32(r.GetOrdinal("location_id")),
                                Amount = r.GetDecimal(r.GetOrdinal("amount")),
                                ClaimedAt = r.GetDateTime(r.GetOrdinal("claimed_at")),
                                ClaimedBy = r.IsDBNull(r.GetOrdinal("claimed_by")) ? null : r.GetInt64(r.GetOrdinal("claimed_by")),
                                ClaimNotes = r.IsDBNull(r.GetOrdinal("claim_notes")) ? null : r.GetString(r.GetOrdinal("claim_notes")),
                                InvestmentTypeId = r.GetInt32(r.GetOrdinal("investment_type_id")),
                                StaffId = r.IsDBNull(r.GetOrdinal("staff_id")) ? null : r.GetInt32(r.GetOrdinal("staff_id")),
                            });
                        }
                    }
                }
                return new ApiResponse<List<InvestmentClaimRowDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentClaimRowDto>> { Success = false, Message = ex.Message };
            }
        }
    }
}
