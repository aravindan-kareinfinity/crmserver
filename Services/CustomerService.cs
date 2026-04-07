using CRM.Server.DTOs;
using CRM.Server.Models;
using Npgsql;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>CustomerService.ts</c> (getAll, getById, getByType, CRUD, getTimeline).</summary>
    public interface ICustomerService
    {
        Task<ApiResponse<PaginatedResponse<CustomerResponseDto>>> GetAllCustomers(int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
        Task<ApiResponse<List<CustomerResponseDto>>> GetAllCustomersList();
        Task<ApiResponse<CustomerResponseDto>> GetCustomerById(int id);
        Task<ApiResponse<CustomerResponseDto>> GetCustomerByCode(string code);
        Task<ApiResponse<CustomerResponseDto>> CreateCustomer(CreateCustomerDto dto);
        Task<ApiResponse<CustomerResponseDto>> UpdateCustomer(int id, UpdateCustomerDto dto);
        Task<ApiResponse<bool>> DeleteCustomer(int id);
        Task<ApiResponse<List<CustomerResponseDto>>> GetCustomersByTypeId(int typeId);
        /// <summary>UI <c>getByType('lead' | 'prospect' | 'customer')</c> — resolves reference_entries (category "Customer Type").</summary>
        Task<ApiResponse<List<CustomerResponseDto>>> GetCustomersByType(string type);
        Task<ApiResponse<List<CustomerTimelineEntryDto>>> GetCustomerTimeline(int customerId);
        Task<ApiResponse<List<CustomerTimelineEntryDto>>> GetCustomerTimelineByCustomerCode(string customerCode);
        Task<ApiResponse<CustomerTimelineEntryDto>> AddCustomerTimelineEntry(int customerId, AddTimelineEntryDto dto);
        Task<ApiResponse<CustomerTimelineEntryDto>> AddCustomerTimelineEntryByCustomerCode(string customerCode, AddTimelineEntryDto dto);
        /// <summary>Parse .xlsx (first sheet), validate like SPA bulk rules, insert all in one transaction or return row errors.</summary>
        Task<ApiResponse<BulkImportCustomersResultDto>> ImportCustomersFromSpreadsheetAsync(Stream stream, long? userId = null);
    }

    public partial class CustomerService : ICustomerService
    {
        IDbProvider dbprovider;

        public CustomerService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        private static string DescribeException(Exception ex)
        {
            // Prefer PostgreSQL details if present (FK violations, missing columns, etc.)
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                if (cur is PostgresException pg)
                {
                    var where = string.IsNullOrWhiteSpace(pg.Where) ? "" : $" | where: {pg.Where}";
                    var detail = string.IsNullOrWhiteSpace(pg.Detail) ? "" : $" | detail: {pg.Detail}";
                    return $"PostgreSQL {pg.SqlState}: {pg.MessageText}{detail}{where}";
                }
            }

            // Otherwise include the exception chain messages.
            var parts = new List<string>();
            for (var cur = ex; cur != null; cur = cur.InnerException)
                parts.Add(cur.Message);
            return string.Join(" | inner: ", parts);
        }

        private static string FormatUserDisplayName(string firstName, string lastName, string email, string userLoginId)
        {
            var full = $"{firstName} {lastName}".Trim();
            if (full.Length > 0) return full;
            if (!string.IsNullOrWhiteSpace(email)) return email.Trim();
            if (!string.IsNullOrWhiteSpace(userLoginId)) return userLoginId.Trim();
            return string.Empty;
        }

        private async Task<Dictionary<long, string>> LoadCreatorDisplayNamesAsync(IEnumerable<long?> userIds)
        {
            var ints = userIds
                .Where(id => id.HasValue && id.Value > 0 && id.Value <= int.MaxValue)
                .Select(id => (int)id!.Value)
                .Distinct()
                .ToList();
            if (ints.Count == 0)
                return new Dictionary<long, string>();

            var map = new Dictionary<long, string>();
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                // Build IN list safely by expanding parameters.
                var paramNames = ints.Select((_, i) => $"@u{i}").ToList();
                var sql = $@"
SELECT id, first_name, last_name, email, user_id
FROM users
WHERE id IN ({string.Join(", ", paramNames)});";
                var cmd = db.GetCommand(sql);
                for (int i = 0; i < ints.Count; i++)
                    db.AddParameter(cmd, $"u{i}", DbTypes.Types.Integer).Value = ints[i];

                using (DbDataReader reader = await db.Execute(cmd))
                {
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetInt32(reader.GetOrdinal("id"));
                        var first = reader.GetString(reader.GetOrdinal("first_name"));
                        var last = reader.GetString(reader.GetOrdinal("last_name"));
                        var email = reader.GetString(reader.GetOrdinal("email"));
                        var login = reader.GetString(reader.GetOrdinal("user_id"));
                        var label = FormatUserDisplayName(first, last, email, login);
                        if (label.Length > 0)
                            map[(long)id] = label;
                    }
                }
            }

            return map;
        }

        private static string? ResolveCreatorName(long? createdBy, IReadOnlyDictionary<long, string> names)
        {
            if (createdBy is null) return null;
            if (names.TryGetValue(createdBy.Value, out var n) && !string.IsNullOrWhiteSpace(n))
                return n;
            return createdBy.Value == AuditUserIds.System ? "System" : null;
        }

        private static string? ResolveCreatorName(long createdBy, IReadOnlyDictionary<long, string> names) =>
            names.TryGetValue(createdBy, out var n) && !string.IsNullOrWhiteSpace(n)
                ? n
                : createdBy == AuditUserIds.System
                    ? "System"
                    : null;

        private static IEnumerable<long?> CustomerDisplayUserIds(Customer c)
        {
            yield return c.CreatedBy;
            yield return c.ProspectConvertedBy;
            yield return c.CustomerConvertedBy;
        }

        private static IEnumerable<long?> CustomerDisplayUserIdsMany(IEnumerable<Customer> customers) =>
            customers.SelectMany(CustomerDisplayUserIds);

        /// <summary>Second key for <c>pg_advisory_xact_lock</c> (customer code sequence).</summary>
        private const int CustomerCodeAdvisoryLockKey2 = 0x4372_6D63;

        private static async Task<int> GetMaxCustomerSequenceForYearAsync(IDb db, int year)
        {
            var prefix = $"{year}/";
            List<string> codes = new();
            var cmd = db.GetCommand("SELECT code FROM customers WHERE code LIKE @prefix;");
            db.AddParameter(cmd, "prefix", DbTypes.Types.String).Value = prefix + "%";
            using (DbDataReader reader = await db.Execute(cmd))
            {
                while (await reader.ReadAsync())
                    codes.Add(reader.GetString(reader.GetOrdinal("code")));
            }
            var max = 0;
            foreach (var code in codes)
            {
                var slash = code.IndexOf('/');
                if (slash < 0 || slash >= code.Length - 1)
                    continue;
                var tail = code[(slash + 1)..].Trim();
                if (int.TryParse(tail, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seq) && seq > max)
                    max = seq;
            }

            return max;
        }

        /// <summary>
        /// Reserve sequential customer codes (<c>2026/0001</c>, …). Must run inside an open transaction; uses a per-year PostgreSQL advisory lock.
        /// </summary>
        private static async Task<IReadOnlyList<string>> ReserveCustomerCodesAsync(IDb db, int year, int count)
        {
            if (count <= 0)
                return Array.Empty<string>();

            var lockCmd = db.GetCommand("SELECT pg_advisory_xact_lock(@k1, @k2);");
            db.AddParameter(lockCmd, "k1", DbTypes.Types.Integer).Value = year;
            db.AddParameter(lockCmd, "k2", DbTypes.Types.Integer).Value = CustomerCodeAdvisoryLockKey2;
            using (DbDataReader reader = await db.Execute(lockCmd))
            {
                // no-op; just ensure command is executed
                await reader.ReadAsync();
            }

            var max = await GetMaxCustomerSequenceForYearAsync(db, year);
            var list = new List<string>(count);
            for (var i = 1; i <= count; i++)
                list.Add($"{year}/{max + i:D4}");
            return list;
        }

        /// <summary>Lowest active <see cref="ReferenceEntry.Id"/> in category (same rule as bulk import defaults).</summary>
        private async Task<int?> FirstActiveRefIdByCategoryAsync(string category) =>
            await FirstActiveRefIdByCategorySqlAsync(category);

        private async Task<int?> FirstActiveRefIdByCategorySqlAsync(string category)
        {
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var cmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE is_active = true AND category = @category
ORDER BY id
LIMIT 1;");
                db.AddParameter(cmd, "category", DbTypes.Types.String).Value = category;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (await reader.ReadAsync())
                        return reader.GetInt32(reader.GetOrdinal("id"));
                }
            }
            return null;
        }

        private static CustomerResponseDto MapCustomer(Customer c, IReadOnlyDictionary<long, string> names) => new()
        {
            Id = c.Id,
            Code = c.Code,
            RegName = c.RegName,
            Mobile = c.Mobile,
            Email = c.Email,
            BusinessTypeId = c.BusinessTypeId,
            IndustryId = c.IndustryId,
            LeadSourceId = c.LeadSourceId,
            AddressLine1 = c.AddressLine1,
            AddressLine2 = c.AddressLine2,
            CityId = c.CityId,
            StateId = c.StateId,
            CountryId = c.CountryId,
            Pincode = c.Pincode,
            GstNumber = c.GstNumber,
            ContactPersons = c.ContactPersons,
            Emails = c.Emails,
            Mobiles = c.Mobiles,
            ShopSizeId = c.ShopSizeId,
            TierId = c.TierId,
            TypeId = c.TypeId,
            IsActive = c.IsActive,
            TotalLocations = c.TotalLocations,
            TotalTradeNames = c.TotalTradeNames,
            CreatedAt = c.CreatedAt,
            CreatedBy = c.CreatedBy,
            CreatedByName = ResolveCreatorName(c.CreatedBy, names),
            ModifiedAt = c.ModifiedAt,
            ModifiedBy = c.ModifiedBy,
            ConvertedAt = c.ConvertedAt,
            ConvertedBy = c.ConvertedBy,
            ProspectConvertedAt = c.ProspectConvertedAt,
            ProspectConvertedBy = c.ProspectConvertedBy,
            ProspectConvertedByName = ResolveCreatorName(c.ProspectConvertedBy, names),
            CustomerConvertedAt = c.CustomerConvertedAt,
            CustomerConvertedBy = c.CustomerConvertedBy,
            CustomerConvertedByName = ResolveCreatorName(c.CustomerConvertedBy, names),
            PipelineStatus = c.PipelineStatus,
            ProductFeaturesDiscussed = c.ProductFeaturesDiscussed,
            AssignedRepresentativeId = c.AssignedRepresentativeId,
            InteractionModeId = c.InteractionModeId,
            PricePlanSelected = c.PricePlanSelected,
            QuotationPreparedSent = c.QuotationPreparedSent,
            QuotationAccepted = c.QuotationAccepted,
            AdvancePaymentReceived = c.AdvancePaymentReceived,
            InvoiceGenerated = c.InvoiceGenerated,
            InvoiceNumber = c.InvoiceNumber
        };

        private async Task<string?> GetCustomerTypeValueLowerAsync(int typeId)
        {
            string? v = null;
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var cmd = db.GetCommand(@"
SELECT value
FROM reference_entries
WHERE id = @id AND category = 'Customer Type'
LIMIT 1;");
                db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = typeId;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (await reader.ReadAsync())
                        v = reader.IsDBNull(reader.GetOrdinal("value")) ? null : reader.GetString(reader.GetOrdinal("value"));
                }
            }
            return string.IsNullOrWhiteSpace(v) ? null : v.ToLowerInvariant();
        }

        private async Task<string> ResolveUserAuditLabelAsync(long userId)
        {
            if (userId <= 0) return "System";
            var names = await LoadCreatorDisplayNamesAsync(new long?[] { userId });
            return ResolveCreatorName(userId, names) ?? $"#{userId}";
        }

        private async Task<long?> ResolveAuditUserIdForFkAsync(long userId)
        {
            // Some columns in customers FK to users(id) and will fail if the id doesn't exist.
            if (userId > 0 && userId <= int.MaxValue)
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT 1 FROM users WHERE id=@id LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = (int)userId;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            return userId;
                    }
                }
            }

            // Prefer the "System" user if present; otherwise skip setting FK fields.
            if (AuditUserIds.System > 0 && AuditUserIds.System <= int.MaxValue)
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT 1 FROM users WHERE id=@id LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = (int)AuditUserIds.System;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            return AuditUserIds.System;
                    }
                }
            }

            return null;
        }

        /// <summary>Sets prospect/customer conversion timestamps when <see cref="Customer.TypeId"/> changes (lead → prospect → customer).</summary>
        private async Task ApplyCustomerTypeConversionAsync(Customer customer, int previousTypeId, long auditUserId)
        {
            if (customer.TypeId == previousTypeId) return;
            var oldVal = await GetCustomerTypeValueLowerAsync(previousTypeId);
            var newVal = await GetCustomerTypeValueLowerAsync(customer.TypeId);
            if (string.IsNullOrEmpty(oldVal) || string.IsNullOrEmpty(newVal)) return;

            var now = DateTime.UtcNow;
            var fkAuditUserId = await ResolveAuditUserIdForFkAsync(auditUserId);
            if (newVal == "prospect" && oldVal == "lead")
            {
                customer.ProspectConvertedAt = now;
                customer.ProspectConvertedBy = fkAuditUserId;
            }
            else if (newVal == "customer" && oldVal == "prospect")
            {
                customer.CustomerConvertedAt = now;
                customer.CustomerConvertedBy = fkAuditUserId;
                customer.ConvertedAt = now;
                customer.ConvertedBy = await ResolveUserAuditLabelAsync(auditUserId);
            }
            else if (newVal == "customer" && oldVal == "lead")
            {
                customer.ProspectConvertedAt = now;
                customer.ProspectConvertedBy = fkAuditUserId;
                customer.CustomerConvertedAt = now;
                customer.CustomerConvertedBy = fkAuditUserId;
                customer.ConvertedAt = now;
                customer.ConvertedBy = await ResolveUserAuditLabelAsync(auditUserId);
            }
        }

        private static List<string> SplitCsvToList(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static string JoinListToCsv(List<string>? list) =>
            list == null ? string.Empty : string.Join(",", list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));

        private static Customer ReadCustomer(DbDataReader reader)
        {
            var c = new Customer
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Code = reader.GetString(reader.GetOrdinal("code")),
                RegName = reader.GetString(reader.GetOrdinal("reg_name")),
                Mobile = reader.GetString(reader.GetOrdinal("mobile")),
                Email = reader.GetString(reader.GetOrdinal("email")),
                BusinessTypeId = reader.IsDBNull(reader.GetOrdinal("business_type_id")) ? null : reader.GetInt32(reader.GetOrdinal("business_type_id")),
                IndustryId = reader.IsDBNull(reader.GetOrdinal("industry_id")) ? null : reader.GetInt32(reader.GetOrdinal("industry_id")),
                LeadSourceId = reader.IsDBNull(reader.GetOrdinal("lead_source_id")) ? null : reader.GetInt32(reader.GetOrdinal("lead_source_id")),
                AddressLine1 = reader.GetString(reader.GetOrdinal("address_line1")),
                AddressLine2 = reader.IsDBNull(reader.GetOrdinal("address_line2")) ? null : reader.GetString(reader.GetOrdinal("address_line2")),
                CityId = reader.IsDBNull(reader.GetOrdinal("city_id")) ? null : reader.GetInt32(reader.GetOrdinal("city_id")),
                StateId = reader.IsDBNull(reader.GetOrdinal("state_id")) ? null : reader.GetInt32(reader.GetOrdinal("state_id")),
                CountryId = reader.IsDBNull(reader.GetOrdinal("country_id")) ? null : reader.GetInt32(reader.GetOrdinal("country_id")),
                Pincode = reader.GetString(reader.GetOrdinal("pincode")),
                GstNumber = reader.IsDBNull(reader.GetOrdinal("gst_number")) ? null : reader.GetString(reader.GetOrdinal("gst_number")),
                ContactPersons = SplitCsvToList(reader.IsDBNull(reader.GetOrdinal("contact_persons")) ? null : reader.GetString(reader.GetOrdinal("contact_persons"))),
                Emails = SplitCsvToList(reader.IsDBNull(reader.GetOrdinal("emails")) ? null : reader.GetString(reader.GetOrdinal("emails"))),
                Mobiles = SplitCsvToList(reader.IsDBNull(reader.GetOrdinal("mobiles")) ? null : reader.GetString(reader.GetOrdinal("mobiles"))),
                ShopSizeId = reader.GetInt32(reader.GetOrdinal("shop_size_id")),
                TierId = reader.GetInt32(reader.GetOrdinal("tier_id")),
                TypeId = reader.GetInt32(reader.GetOrdinal("type_id")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                TotalLocations = reader.IsDBNull(reader.GetOrdinal("total_locations")) ? null : reader.GetInt32(reader.GetOrdinal("total_locations")),
                TotalTradeNames = reader.IsDBNull(reader.GetOrdinal("total_trade_names")) ? null : reader.GetInt32(reader.GetOrdinal("total_trade_names")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                ConvertedAt = reader.IsDBNull(reader.GetOrdinal("converted_at")) ? null : reader.GetDateTime(reader.GetOrdinal("converted_at")),
                ConvertedBy = reader.IsDBNull(reader.GetOrdinal("converted_by")) ? null : reader.GetString(reader.GetOrdinal("converted_by")),
                ProspectConvertedAt = reader.IsDBNull(reader.GetOrdinal("prospect_converted_at")) ? null : reader.GetDateTime(reader.GetOrdinal("prospect_converted_at")),
                ProspectConvertedBy = reader.IsDBNull(reader.GetOrdinal("prospect_converted_by")) ? null : reader.GetInt64(reader.GetOrdinal("prospect_converted_by")),
                CustomerConvertedAt = reader.IsDBNull(reader.GetOrdinal("customer_converted_at")) ? null : reader.GetDateTime(reader.GetOrdinal("customer_converted_at")),
                CustomerConvertedBy = reader.IsDBNull(reader.GetOrdinal("customer_converted_by")) ? null : reader.GetInt64(reader.GetOrdinal("customer_converted_by")),
                PipelineStatus = reader.IsDBNull(reader.GetOrdinal("pipeline_status")) ? null : reader.GetString(reader.GetOrdinal("pipeline_status")),
                ProductFeaturesDiscussed = reader.GetBoolean(reader.GetOrdinal("product_features_discussed")),
                AssignedRepresentativeId = reader.IsDBNull(reader.GetOrdinal("assigned_representative_id")) ? null : reader.GetInt64(reader.GetOrdinal("assigned_representative_id")),
                InteractionModeId = reader.IsDBNull(reader.GetOrdinal("interaction_mode_id")) ? null : reader.GetInt32(reader.GetOrdinal("interaction_mode_id")),
                PricePlanSelected = reader.GetBoolean(reader.GetOrdinal("price_plan_selected")),
                QuotationPreparedSent = reader.GetBoolean(reader.GetOrdinal("quotation_prepared_sent")),
                QuotationAccepted = reader.GetBoolean(reader.GetOrdinal("quotation_accepted")),
                AdvancePaymentReceived = reader.GetBoolean(reader.GetOrdinal("advance_payment_received")),
                InvoiceGenerated = reader.GetBoolean(reader.GetOrdinal("invoice_generated")),
                InvoiceNumber = reader.IsDBNull(reader.GetOrdinal("invoice_number")) ? null : reader.GetString(reader.GetOrdinal("invoice_number")),
                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
            };
            return c;
        }

        private async Task<string?> GetCustomerCodeByIdAsync(int customerId)
        {
            if (customerId <= 0) return null;
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var cmd = db.GetCommand("SELECT code FROM customers WHERE id=@id LIMIT 1;");
                db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = customerId;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (await reader.ReadAsync())
                        return reader.IsDBNull(reader.GetOrdinal("code")) ? null : reader.GetString(reader.GetOrdinal("code"));
                }
            }
            return null;
        }

        private async Task<(int CustomerId, string? Error)> ResolveCustomerIdAsync(int customerId, string? customerCode)
        {
            if (!string.IsNullOrWhiteSpace(customerCode))
            {
                var trimmed = customerCode.Trim();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT id FROM customers WHERE code=@code LIMIT 1;");
                    db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            return (reader.GetInt32(reader.GetOrdinal("id")), null);
                    }
                }
                return (0, $"Unknown customer code: \"{trimmed}\"");
            }

            if (customerId <= 0)
                return (0, "Provide customerId or customerCode");
            return (customerId, null);
        }

        public async Task<ApiResponse<PaginatedResponse<CustomerResponseDto>>> GetAllCustomers(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
        {
            try
            {
                var offset = Math.Max(0, (pageNumber - 1) * pageSize);
                var where = "WHERE is_active=true";
                var hasSearch = !string.IsNullOrWhiteSpace(searchTerm);
                if (hasSearch)
                    where += " AND (reg_name ILIKE @q OR email ILIKE @q OR code ILIKE @q)";

                int total = 0;
                var items = new List<Customer>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var countCmd = db.GetCommand($"SELECT COUNT(*)::int AS total FROM customers {where};");
                    if (hasSearch)
                        db.AddParameter(countCmd, "q", DbTypes.Types.String).Value = "%" + searchTerm!.Trim() + "%";
                    using (DbDataReader r = await db.Execute(countCmd))
                    {
                        if (await r.ReadAsync())
                            total = r.GetInt32(r.GetOrdinal("total"));
                    }

                    var listCmd = db.GetCommand($@"
SELECT *
FROM customers
{where}
ORDER BY id DESC
LIMIT @limit OFFSET @offset;");
                    if (hasSearch)
                        db.AddParameter(listCmd, "q", DbTypes.Types.String).Value = "%" + searchTerm!.Trim() + "%";
                    db.AddParameter(listCmd, "limit", DbTypes.Types.Integer).Value = pageSize;
                    db.AddParameter(listCmd, "offset", DbTypes.Types.Integer).Value = offset;
                    using (DbDataReader r2 = await db.Execute(listCmd))
                    {
                        while (await r2.ReadAsync())
                            items.Add(ReadCustomer(r2));
                    }
                }

                var creatorNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIdsMany(items));

                return new ApiResponse<PaginatedResponse<CustomerResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<CustomerResponseDto>
                    {
                        Items = items.Select(c => MapCustomer(c, creatorNames)).ToList(),
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<CustomerResponseDto>> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<List<CustomerResponseDto>>> GetAllCustomersList()
        {
            try
            {
                var list = new List<Customer>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM customers WHERE is_active=true ORDER BY id DESC;");
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        while (await reader.ReadAsync())
                            list.Add(ReadCustomer(reader));
                    }
                }
                var creatorNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIdsMany(list));
                return new ApiResponse<List<CustomerResponseDto>>
                {
                    Success = true,
                    Data = list.Select(c => MapCustomer(c, creatorNames)).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> GetCustomerById(int id)
        {
            try
            {
                Customer? customer = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            customer = ReadCustomer(reader);
                    }
                }
                if (customer == null)
                    return new ApiResponse<CustomerResponseDto> { Success = false, Message = "Customer not found" };
                var creatorNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIds(customer));
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = true,
                    Data = MapCustomer(customer, creatorNames)
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> GetCustomerByCode(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                    return new ApiResponse<CustomerResponseDto> { Success = false, Message = "customerCode is required" };
                var trimmed = code.Trim();
                Customer? customer = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
                    db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            customer = ReadCustomer(reader);
                    }
                }
                if (customer == null)
                    return new ApiResponse<CustomerResponseDto> { Success = false, Message = "Customer not found" };
                var creatorNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIds(customer));
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = true,
                    Data = MapCustomer(customer, creatorNames)
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> CreateCustomer(CreateCustomerDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RegName))
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = "Registered name is required" };

            var shopSizeId = dto.ShopSizeId > 0
                ? dto.ShopSizeId
                : await FirstActiveRefIdByCategoryAsync("Shop Size") ?? 0;
            if (shopSizeId <= 0)
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = false,
                    Message = "No Shop Size reference data; add one under reference data or select a shop size."
                };

            var tierId = dto.TierId > 0
                ? dto.TierId
                : await FirstActiveRefIdByCategoryAsync("City Tier") ?? 0;
            if (tierId <= 0)
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = false,
                    Message = "No City Tier reference data; add one under reference data or select a city tier."
                };

            const int defaultLeadTypeId = 88;
            var typeId = dto.TypeId > 0 ? dto.TypeId : defaultLeadTypeId;

            try
            {
                var now = DateTime.UtcNow;
                var year = now.Year;
                var auditUserId = dto.CreatedByUserId.HasValue && dto.CreatedByUserId.Value > 0
                    ? dto.CreatedByUserId.Value
                    : AuditUserIds.System;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        var codes = await ReserveCustomerCodesAsync(db, year, 1);
                        var insert = db.GetCommand(@"
INSERT INTO customers (
    code, reg_name, mobile, email,
    business_type_id, industry_id, lead_source_id,
    address_line1, address_line2,
    city_id, state_id, country_id,
    pincode, gst_number,
    contact_persons, emails, mobiles,
    shop_size_id, tier_id, type_id,
    is_active, total_locations, total_trade_names,
    created_at, created_by,
    converted_at, converted_by,
    prospect_converted_at, prospect_converted_by,
    customer_converted_at, customer_converted_by,
    pipeline_status, product_features_discussed,
    assigned_representative_id, interaction_mode_id,
    price_plan_selected, quotation_prepared_sent, quotation_accepted,
    advance_payment_received, invoice_generated, invoice_number,
    modified_at, modified_by
)
VALUES (
    @code, @reg_name, @mobile, @email,
    @business_type_id, @industry_id, @lead_source_id,
    @address_line1, @address_line2,
    @city_id, @state_id, @country_id,
    @pincode, @gst_number,
    @contact_persons, @emails, @mobiles,
    @shop_size_id, @tier_id, @type_id,
    true, NULL, NULL,
    @created_at, @created_by,
    NULL, NULL,
    NULL, NULL,
    NULL, NULL,
    NULL, @product_features_discussed,
    @assigned_representative_id, @interaction_mode_id,
    @price_plan_selected, @quotation_prepared_sent, @quotation_accepted,
    @advance_payment_received, @invoice_generated, @invoice_number,
    @modified_at, @modified_by
)
RETURNING id;");
                        db.AddParameter(insert, "code", DbTypes.Types.String).Value = codes[0];
                        db.AddParameter(insert, "reg_name", DbTypes.Types.String).Value = dto.RegName.Trim();
                        db.AddParameter(insert, "mobile", DbTypes.Types.String).Value = dto.Mobile ?? string.Empty;
                        db.AddParameter(insert, "email", DbTypes.Types.String).Value = dto.Email ?? string.Empty;
                        db.AddParameter(insert, "business_type_id", DbTypes.Types.Integer).Value = dto.BusinessTypeId.HasValue ? dto.BusinessTypeId.Value : DBNull.Value;
                        db.AddParameter(insert, "industry_id", DbTypes.Types.Integer).Value = dto.IndustryId.HasValue ? dto.IndustryId.Value : DBNull.Value;
                        db.AddParameter(insert, "lead_source_id", DbTypes.Types.Integer).Value = dto.LeadSourceId.HasValue ? dto.LeadSourceId.Value : DBNull.Value;
                        db.AddParameter(insert, "address_line1", DbTypes.Types.String).Value = dto.AddressLine1 ?? string.Empty;
                        db.AddParameter(insert, "address_line2", DbTypes.Types.String).Value = dto.AddressLine2 ?? (object)DBNull.Value;
                        db.AddParameter(insert, "city_id", DbTypes.Types.Integer).Value = dto.CityId.HasValue ? dto.CityId.Value : DBNull.Value;
                        db.AddParameter(insert, "state_id", DbTypes.Types.Integer).Value = dto.StateId.HasValue ? dto.StateId.Value : DBNull.Value;
                        db.AddParameter(insert, "country_id", DbTypes.Types.Integer).Value = dto.CountryId.HasValue ? dto.CountryId.Value : DBNull.Value;
                        db.AddParameter(insert, "pincode", DbTypes.Types.String).Value = dto.Pincode ?? string.Empty;
                        db.AddParameter(insert, "gst_number", DbTypes.Types.String).Value = dto.GstNumber ?? (object)DBNull.Value;
                        db.AddParameter(insert, "contact_persons", DbTypes.Types.String).Value = JoinListToCsv(dto.ContactPersons);
                        db.AddParameter(insert, "emails", DbTypes.Types.String).Value = JoinListToCsv(dto.Emails);
                        db.AddParameter(insert, "mobiles", DbTypes.Types.String).Value = JoinListToCsv(dto.Mobiles);
                        db.AddParameter(insert, "shop_size_id", DbTypes.Types.Integer).Value = shopSizeId;
                        db.AddParameter(insert, "tier_id", DbTypes.Types.Integer).Value = tierId;
                        db.AddParameter(insert, "type_id", DbTypes.Types.Integer).Value = typeId;
                        db.AddParameter(insert, "created_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(insert, "created_by", DbTypes.Types.Long).Value = auditUserId;
                        db.AddParameter(insert, "product_features_discussed", DbTypes.Types.Boolean).Value = dto.ProductFeaturesDiscussed ?? false;
                        db.AddParameter(insert, "assigned_representative_id", DbTypes.Types.Long).Value = dto.AssignedRepresentativeId.HasValue ? dto.AssignedRepresentativeId.Value : DBNull.Value;
                        db.AddParameter(insert, "interaction_mode_id", DbTypes.Types.Integer).Value = dto.InteractionModeId.HasValue ? dto.InteractionModeId.Value : DBNull.Value;
                        db.AddParameter(insert, "price_plan_selected", DbTypes.Types.Boolean).Value = dto.PricePlanSelected ?? false;
                        db.AddParameter(insert, "quotation_prepared_sent", DbTypes.Types.Boolean).Value = dto.QuotationPreparedSent ?? false;
                        db.AddParameter(insert, "quotation_accepted", DbTypes.Types.Boolean).Value = dto.QuotationAccepted ?? false;
                        db.AddParameter(insert, "advance_payment_received", DbTypes.Types.Boolean).Value = dto.AdvancePaymentReceived ?? false;
                        db.AddParameter(insert, "invoice_generated", DbTypes.Types.Boolean).Value = dto.InvoiceGenerated ?? false;
                        db.AddParameter(insert, "invoice_number", DbTypes.Types.String).Value =
                            string.IsNullOrWhiteSpace(dto.InvoiceNumber) ? (object)DBNull.Value : dto.InvoiceNumber.Trim();
                        db.AddParameter(insert, "modified_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(insert, "modified_by", DbTypes.Types.Long).Value = auditUserId;

                        int newId = 0;
                        using (DbDataReader rr = await db.Execute(insert))
                        {
                            if (await rr.ReadAsync())
                                newId = rr.GetInt32(rr.GetOrdinal("id"));
                        }
                        await db.CommitTransaction();
                        return await GetCustomerById(newId);
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
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> UpdateCustomer(int id, UpdateCustomerDto dto)
        {
            try
            {
                Customer? customer = null;
                using (IDb db0 = await dbprovider.GetDb())
                {
                    await db0.Connect();
                    var cmd0 = db0.GetCommand("SELECT * FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
                    db0.AddParameter(cmd0, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader reader = await db0.Execute(cmd0))
                    {
                        if (await reader.ReadAsync())
                            customer = ReadCustomer(reader);
                    }
                }
                if (customer == null)
                    return new ApiResponse<CustomerResponseDto> { Success = false, Message = "Customer not found" };

                var previousTypeId = customer.TypeId;

                if (!string.IsNullOrWhiteSpace(dto.RegName)) customer.RegName = dto.RegName;
                if (!string.IsNullOrWhiteSpace(dto.Mobile)) customer.Mobile = dto.Mobile;
                if (!string.IsNullOrWhiteSpace(dto.Email)) customer.Email = dto.Email;
                if (dto.BusinessTypeId.HasValue) customer.BusinessTypeId = dto.BusinessTypeId;
                if (dto.IndustryId.HasValue) customer.IndustryId = dto.IndustryId;
                if (dto.LeadSourceId.HasValue) customer.LeadSourceId = dto.LeadSourceId;
                if (!string.IsNullOrWhiteSpace(dto.AddressLine1)) customer.AddressLine1 = dto.AddressLine1;
                if (dto.AddressLine2 != null) customer.AddressLine2 = dto.AddressLine2;
                if (dto.CityId.HasValue) customer.CityId = dto.CityId;
                if (dto.StateId.HasValue) customer.StateId = dto.StateId;
                if (dto.CountryId.HasValue) customer.CountryId = dto.CountryId;
                if (!string.IsNullOrWhiteSpace(dto.Pincode)) customer.Pincode = dto.Pincode;
                if (dto.GstNumber != null) customer.GstNumber = dto.GstNumber;
                if (dto.ShopSizeId is > 0) customer.ShopSizeId = dto.ShopSizeId.Value;
                if (dto.TierId is > 0) customer.TierId = dto.TierId.Value;
                if (dto.ContactPersons != null) customer.ContactPersons = dto.ContactPersons;
                if (dto.Emails != null) customer.Emails = dto.Emails;
                if (dto.Mobiles != null) customer.Mobiles = dto.Mobiles;
                if (dto.TypeId is > 0) customer.TypeId = dto.TypeId.Value;
                if (dto.IsActive.HasValue) customer.IsActive = dto.IsActive.Value;
                if (dto.ConvertedAt.HasValue) customer.ConvertedAt = dto.ConvertedAt;
                if (dto.ConvertedBy != null) customer.ConvertedBy = dto.ConvertedBy;
                if (dto.PipelineStatus != null)
                    customer.PipelineStatus = string.IsNullOrWhiteSpace(dto.PipelineStatus)
                        ? null
                        : dto.PipelineStatus.Trim();
                if (dto.ProductFeaturesDiscussed.HasValue) customer.ProductFeaturesDiscussed = dto.ProductFeaturesDiscussed.Value;
                if (dto.AssignedRepresentativeId.HasValue) customer.AssignedRepresentativeId = dto.AssignedRepresentativeId;
                if (dto.InteractionModeId.HasValue) customer.InteractionModeId = dto.InteractionModeId;
                if (dto.PricePlanSelected.HasValue) customer.PricePlanSelected = dto.PricePlanSelected.Value;
                if (dto.QuotationPreparedSent.HasValue) customer.QuotationPreparedSent = dto.QuotationPreparedSent.Value;
                if (dto.QuotationAccepted.HasValue) customer.QuotationAccepted = dto.QuotationAccepted.Value;
                if (dto.AdvancePaymentReceived.HasValue) customer.AdvancePaymentReceived = dto.AdvancePaymentReceived.Value;
                if (dto.InvoiceGenerated.HasValue) customer.InvoiceGenerated = dto.InvoiceGenerated.Value;
                if (dto.InvoiceNumber != null)
                    customer.InvoiceNumber = string.IsNullOrWhiteSpace(dto.InvoiceNumber) ? null : dto.InvoiceNumber.Trim();

                var auditUserId = dto.ModifiedByUserId.HasValue && dto.ModifiedByUserId.Value > 0
                    ? dto.ModifiedByUserId.Value
                    : AuditUserIds.System;

                await ApplyCustomerTypeConversionAsync(customer, previousTypeId, auditUserId);

                customer.ModifiedAt = DateTime.UtcNow;
                customer.ModifiedBy = auditUserId;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var update = db.GetCommand(@"
UPDATE customers SET
    reg_name=@reg_name,
    mobile=@mobile,
    email=@email,
    business_type_id=@business_type_id,
    industry_id=@industry_id,
    lead_source_id=@lead_source_id,
    address_line1=@address_line1,
    address_line2=@address_line2,
    city_id=@city_id,
    state_id=@state_id,
    country_id=@country_id,
    pincode=@pincode,
    gst_number=@gst_number,
    contact_persons=@contact_persons,
    emails=@emails,
    mobiles=@mobiles,
    shop_size_id=@shop_size_id,
    tier_id=@tier_id,
    type_id=@type_id,
    is_active=@is_active,
    converted_at=@converted_at,
    converted_by=@converted_by,
    prospect_converted_at=@prospect_converted_at,
    prospect_converted_by=@prospect_converted_by,
    customer_converted_at=@customer_converted_at,
    customer_converted_by=@customer_converted_by,
    pipeline_status=@pipeline_status,
    product_features_discussed=@product_features_discussed,
    assigned_representative_id=@assigned_representative_id,
    interaction_mode_id=@interaction_mode_id,
    price_plan_selected=@price_plan_selected,
    quotation_prepared_sent=@quotation_prepared_sent,
    quotation_accepted=@quotation_accepted,
    advance_payment_received=@advance_payment_received,
    invoice_generated=@invoice_generated,
    invoice_number=@invoice_number,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                    db.AddParameter(update, "id", DbTypes.Types.Integer).Value = customer.Id;
                    db.AddParameter(update, "reg_name", DbTypes.Types.String).Value = customer.RegName;
                    db.AddParameter(update, "mobile", DbTypes.Types.String).Value = customer.Mobile;
                    db.AddParameter(update, "email", DbTypes.Types.String).Value = customer.Email;
                    db.AddParameter(update, "business_type_id", DbTypes.Types.Integer).Value = customer.BusinessTypeId.HasValue ? customer.BusinessTypeId.Value : DBNull.Value;
                    db.AddParameter(update, "industry_id", DbTypes.Types.Integer).Value = customer.IndustryId.HasValue ? customer.IndustryId.Value : DBNull.Value;
                    db.AddParameter(update, "lead_source_id", DbTypes.Types.Integer).Value = customer.LeadSourceId.HasValue ? customer.LeadSourceId.Value : DBNull.Value;
                    db.AddParameter(update, "address_line1", DbTypes.Types.String).Value = customer.AddressLine1;
                    db.AddParameter(update, "address_line2", DbTypes.Types.String).Value = customer.AddressLine2 ?? (object)DBNull.Value;
                    db.AddParameter(update, "city_id", DbTypes.Types.Integer).Value = customer.CityId.HasValue ? customer.CityId.Value : DBNull.Value;
                    db.AddParameter(update, "state_id", DbTypes.Types.Integer).Value = customer.StateId.HasValue ? customer.StateId.Value : DBNull.Value;
                    db.AddParameter(update, "country_id", DbTypes.Types.Integer).Value = customer.CountryId.HasValue ? customer.CountryId.Value : DBNull.Value;
                    db.AddParameter(update, "pincode", DbTypes.Types.String).Value = customer.Pincode;
                    db.AddParameter(update, "gst_number", DbTypes.Types.String).Value = customer.GstNumber ?? (object)DBNull.Value;
                    db.AddParameter(update, "contact_persons", DbTypes.Types.String).Value = JoinListToCsv(customer.ContactPersons);
                    db.AddParameter(update, "emails", DbTypes.Types.String).Value = JoinListToCsv(customer.Emails);
                    db.AddParameter(update, "mobiles", DbTypes.Types.String).Value = JoinListToCsv(customer.Mobiles);
                    db.AddParameter(update, "shop_size_id", DbTypes.Types.Integer).Value = customer.ShopSizeId;
                    db.AddParameter(update, "tier_id", DbTypes.Types.Integer).Value = customer.TierId;
                    db.AddParameter(update, "type_id", DbTypes.Types.Integer).Value = customer.TypeId;
                    db.AddParameter(update, "is_active", DbTypes.Types.Boolean).Value = customer.IsActive;
                    db.AddParameter(update, "converted_at", DbTypes.Types.DateTime).Value = customer.ConvertedAt.HasValue ? customer.ConvertedAt.Value : DBNull.Value;
                    db.AddParameter(update, "converted_by", DbTypes.Types.String).Value = customer.ConvertedBy ?? (object)DBNull.Value;
                    db.AddParameter(update, "prospect_converted_at", DbTypes.Types.DateTime).Value = customer.ProspectConvertedAt.HasValue ? customer.ProspectConvertedAt.Value : DBNull.Value;
                    db.AddParameter(update, "prospect_converted_by", DbTypes.Types.Long).Value = customer.ProspectConvertedBy.HasValue ? customer.ProspectConvertedBy.Value : DBNull.Value;
                    db.AddParameter(update, "customer_converted_at", DbTypes.Types.DateTime).Value = customer.CustomerConvertedAt.HasValue ? customer.CustomerConvertedAt.Value : DBNull.Value;
                    db.AddParameter(update, "customer_converted_by", DbTypes.Types.Long).Value = customer.CustomerConvertedBy.HasValue ? customer.CustomerConvertedBy.Value : DBNull.Value;
                    db.AddParameter(update, "pipeline_status", DbTypes.Types.String).Value = customer.PipelineStatus ?? (object)DBNull.Value;
                    db.AddParameter(update, "product_features_discussed", DbTypes.Types.Boolean).Value = customer.ProductFeaturesDiscussed;
                    db.AddParameter(update, "assigned_representative_id", DbTypes.Types.Long).Value = customer.AssignedRepresentativeId.HasValue ? customer.AssignedRepresentativeId.Value : DBNull.Value;
                    db.AddParameter(update, "interaction_mode_id", DbTypes.Types.Integer).Value = customer.InteractionModeId.HasValue ? customer.InteractionModeId.Value : DBNull.Value;
                    db.AddParameter(update, "price_plan_selected", DbTypes.Types.Boolean).Value = customer.PricePlanSelected;
                    db.AddParameter(update, "quotation_prepared_sent", DbTypes.Types.Boolean).Value = customer.QuotationPreparedSent;
                    db.AddParameter(update, "quotation_accepted", DbTypes.Types.Boolean).Value = customer.QuotationAccepted;
                    db.AddParameter(update, "advance_payment_received", DbTypes.Types.Boolean).Value = customer.AdvancePaymentReceived;
                    db.AddParameter(update, "invoice_generated", DbTypes.Types.Boolean).Value = customer.InvoiceGenerated;
                    db.AddParameter(update, "invoice_number", DbTypes.Types.String).Value = customer.InvoiceNumber ?? (object)DBNull.Value;
                    db.AddParameter(update, "modified_at", DbTypes.Types.DateTime).Value = customer.ModifiedAt;
                    db.AddParameter(update, "modified_by", DbTypes.Types.Long).Value = customer.ModifiedBy.HasValue ? customer.ModifiedBy.Value : DBNull.Value;
                    await db.ExecuteNonQuery(update);
                }
                return await GetCustomerById(customer.Id);
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<bool>> DeleteCustomer(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
UPDATE customers
SET is_active=false,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id
RETURNING id;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = DateTime.UtcNow;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<bool> { Success = false, Message = "Customer not found" };
                    }
                    return new ApiResponse<bool> { Success = true, Data = true };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<List<CustomerResponseDto>>> GetCustomersByTypeId(int typeId)
        {
            try
            {
                var list = new List<Customer>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM customers WHERE type_id=@tid AND is_active=true ORDER BY id DESC;");
                    db.AddParameter(cmd, "tid", DbTypes.Types.Integer).Value = typeId;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        while (await reader.ReadAsync())
                            list.Add(ReadCustomer(reader));
                    }
                }
                var creatorNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIdsMany(list));
                return new ApiResponse<List<CustomerResponseDto>>
                {
                    Success = true,
                    Data = list.Select(c => MapCustomer(c, creatorNames)).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<List<CustomerResponseDto>>> GetCustomersByType(string type)
        {
            try
            {
                var t = type.Trim().ToLowerInvariant();
                int typeRefId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE is_active = true
  AND lower(value) = @val
  AND (lower(category) = 'customer type' OR lower(category) = 'customer_type')
ORDER BY id
LIMIT 1;");
                    db.AddParameter(cmd, "val", DbTypes.Types.String).Value = t;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            typeRefId = reader.GetInt32(reader.GetOrdinal("id"));
                    }
                }

                if (typeRefId == 0)
                    return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = $"Unknown customer type: {type}" };

                var list = new List<Customer>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM customers WHERE type_id=@tid AND is_active=true ORDER BY id DESC;");
                    db.AddParameter(cmd, "tid", DbTypes.Types.Integer).Value = typeRefId;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        while (await reader.ReadAsync())
                            list.Add(ReadCustomer(reader));
                    }
                }
                var creatorNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIdsMany(list));
                return new ApiResponse<List<CustomerResponseDto>>
                {
                    Success = true,
                    Data = list.Select(c => MapCustomer(c, creatorNames)).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<List<CustomerTimelineEntryDto>>> GetCustomerTimeline(int customerId)
        {
            try
            {
                var cc = await GetCustomerCodeByIdAsync(customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = true, Data = new List<CustomerTimelineEntryDto>() };
                var rows = new List<CustomerTimelineEntryDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by
FROM customer_timelines
WHERE customer_id=@cid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        while (await reader.ReadAsync())
                        {
                            rows.Add(new CustomerTimelineEntryDto
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                CustomerId = customerId,
                                CustomerCode = cc,
                                Type = reader.GetInt32(reader.GetOrdinal("type")),
                                Notes = reader.GetString(reader.GetOrdinal("notes")),
                                FileId = reader.IsDBNull(reader.GetOrdinal("file_id")) ? null : reader.GetInt32(reader.GetOrdinal("file_id")),
                                FileName = reader.IsDBNull(reader.GetOrdinal("file_name")) ? null : reader.GetString(reader.GetOrdinal("file_name")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            });
                        }
                    }
                }
                var timelineCreatorNames = await LoadCreatorDisplayNamesAsync(rows.Select(x => (long?)x.CreatedBy));
                foreach (var r in rows)
                    r.CreatedByName = ResolveCreatorName(r.CreatedBy, timelineCreatorNames);
                return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<List<CustomerTimelineEntryDto>>> GetCustomerTimelineByCustomerCode(string customerCode)
        {
            try
            {
                var (cid, err) = await ResolveCustomerIdAsync(0, customerCode);
                if (err != null)
                    return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = false, Message = err };
                return await GetCustomerTimeline(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<CustomerTimelineEntryDto>> AddCustomerTimelineEntry(int customerId, AddTimelineEntryDto dto)
        {
            try
            {
                var cc = await GetCustomerCodeByIdAsync(customerId);
                if (string.IsNullOrWhiteSpace(cc))
                    return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = "Customer has no code" };
                var now = DateTime.UtcNow;
                int newId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
INSERT INTO customer_timelines (
    customer_id, customer_code, type, notes, file_id, file_name,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @cid, @cc, @type, @notes, @file_id, @file_name,
    true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                    db.AddParameter(cmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                    db.AddParameter(cmd, "type", DbTypes.Types.Integer).Value = dto.Type;
                    db.AddParameter(cmd, "notes", DbTypes.Types.String).Value = dto.Notes ?? string.Empty;
                    db.AddParameter(cmd, "file_id", DbTypes.Types.Integer).Value = dto.FileId.HasValue ? dto.FileId.Value : DBNull.Value;
                    db.AddParameter(cmd, "file_name", DbTypes.Types.String).Value = dto.FileName ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            newId = reader.GetInt32(reader.GetOrdinal("id"));
                    }
                }

                var entryNames = await LoadCreatorDisplayNamesAsync(new[] { (long?)AuditUserIds.System });
                var entryDto = new CustomerTimelineEntryDto
                {
                    Id = newId,
                    CustomerId = customerId,
                    CustomerCode = cc,
                    Type = dto.Type,
                    Notes = dto.Notes ?? string.Empty,
                    FileId = dto.FileId,
                    FileName = dto.FileName,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    CreatedByName = ResolveCreatorName(AuditUserIds.System, entryNames),
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                return new ApiResponse<CustomerTimelineEntryDto> { Success = true, Data = entryDto };
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<CustomerTimelineEntryDto>> AddCustomerTimelineEntryByCustomerCode(string customerCode, AddTimelineEntryDto dto)
        {
            try
            {
                var (cid, err) = await ResolveCustomerIdAsync(0, customerCode);
                if (err != null)
                    return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = err };
                return await AddCustomerTimelineEntry(cid, dto);
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = DescribeException(ex) };
            }
        }
    }
}
