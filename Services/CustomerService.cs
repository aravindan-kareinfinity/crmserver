using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        private readonly CrmDbContext _context;

        public CustomerService(CrmDbContext context)
        {
            _context = context;
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

            var rows = await _context.Users.AsNoTracking()
                .Where(u => ints.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.UserLoginId })
                .ToListAsync();

            var map = new Dictionary<long, string>();
            foreach (var u in rows)
            {
                var label = FormatUserDisplayName(u.FirstName, u.LastName, u.Email, u.UserLoginId);
                if (label.Length > 0)
                    map[(long)u.Id] = label;
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

        private async Task<int> GetMaxCustomerSequenceForYearAsync(int year)
        {
            var prefix = $"{year}/";
            var codes = await _context.Customers.AsNoTracking()
                .Where(c => c.Code.StartsWith(prefix))
                .Select(c => c.Code)
                .ToListAsync();
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
        private async Task<IReadOnlyList<string>> ReserveCustomerCodesAsync(int year, int count)
        {
            if (count <= 0)
                return Array.Empty<string>();

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({year}, {CustomerCodeAdvisoryLockKey2})");

            var max = await GetMaxCustomerSequenceForYearAsync(year);
            var list = new List<string>(count);
            for (var i = 1; i <= count; i++)
                list.Add($"{year}/{max + i:D4}");
            return list;
        }

        /// <summary>Lowest active <see cref="ReferenceEntry.Id"/> in category (same rule as bulk import defaults).</summary>
        private async Task<int?> FirstActiveRefIdByCategoryAsync(string category) =>
            await _context.ReferenceEntries.AsNoTracking()
                .Where(e => e.IsActive && e.Category == category)
                .OrderBy(e => e.Id)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();

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
            var v = await _context.ReferenceEntries.AsNoTracking()
                .Where(e => e.Id == typeId && e.Category == "Customer Type")
                .Select(e => e.Value)
                .FirstOrDefaultAsync();
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
                var exists = await _context.Users.AsNoTracking().AnyAsync(u => u.Id == (int)userId);
                if (exists) return userId;
            }

            // Prefer the "System" user if present; otherwise skip setting FK fields.
            if (AuditUserIds.System > 0 && AuditUserIds.System <= int.MaxValue)
            {
                var sysExists = await _context.Users.AsNoTracking().AnyAsync(u => u.Id == (int)AuditUserIds.System);
                if (sysExists) return AuditUserIds.System;
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

        public async Task<ApiResponse<PaginatedResponse<CustomerResponseDto>>> GetAllCustomers(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
        {
            try
            {
                var query = _context.Customers.AsQueryable();
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(c =>
                        c.RegName.Contains(searchTerm) ||
                        c.Email.Contains(searchTerm) ||
                        c.Code.Contains(searchTerm));
                }

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

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
                var list = await _context.Customers.OrderByDescending(c => c.CreatedAt).ToListAsync();
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
                var customer = await _context.Customers.FindAsync(id);
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
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Code == trimmed);
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

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var year = now.Year;
                var codes = await ReserveCustomerCodesAsync(year, 1);
                var auditUserId = dto.CreatedByUserId.HasValue && dto.CreatedByUserId.Value > 0
                    ? dto.CreatedByUserId.Value
                    : AuditUserIds.System;
                var customer = new Customer
                {
                    Code = codes[0],
                    RegName = dto.RegName.Trim(),
                    Mobile = dto.Mobile ?? string.Empty,
                    Email = dto.Email ?? string.Empty,
                    BusinessTypeId = dto.BusinessTypeId,
                    IndustryId = dto.IndustryId,
                    LeadSourceId = dto.LeadSourceId,
                    AddressLine1 = dto.AddressLine1 ?? string.Empty,
                    AddressLine2 = dto.AddressLine2,
                    CityId = dto.CityId,
                    StateId = dto.StateId,
                    CountryId = dto.CountryId,
                    Pincode = dto.Pincode ?? string.Empty,
                    GstNumber = dto.GstNumber,
                    ContactPersons = dto.ContactPersons ?? new List<string>(),
                    Emails = dto.Emails ?? new List<string>(),
                    Mobiles = dto.Mobiles ?? new List<string>(),
                    ShopSizeId = shopSizeId,
                    TierId = tierId,
                    TypeId = typeId,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = auditUserId,
                    ModifiedAt = now,
                    ModifiedBy = auditUserId,
                    ProductFeaturesDiscussed = dto.ProductFeaturesDiscussed ?? false,
                    AssignedRepresentativeId = dto.AssignedRepresentativeId,
                    InteractionModeId = dto.InteractionModeId,
                    PricePlanSelected = dto.PricePlanSelected ?? false,
                    QuotationPreparedSent = dto.QuotationPreparedSent ?? false,
                    QuotationAccepted = dto.QuotationAccepted ?? false,
                    AdvancePaymentReceived = dto.AdvancePaymentReceived ?? false,
                    InvoiceGenerated = dto.InvoiceGenerated ?? false,
                    InvoiceNumber = string.IsNullOrWhiteSpace(dto.InvoiceNumber) ? null : dto.InvoiceNumber.Trim()
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                var createdNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIds(customer));
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = true,
                    Message = "Created",
                    Data = MapCustomer(customer, createdNames)
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = DescribeException(ex) };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> UpdateCustomer(int id, UpdateCustomerDto dto)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
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
                await _context.SaveChangesAsync();
                var updatedNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIds(customer));
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = true,
                    Data = MapCustomer(customer, updatedNames)
                };
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
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                    return new ApiResponse<bool> { Success = false, Message = "Customer not found" };
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
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
                var list = await _context.Customers.Where(c => c.TypeId == typeId).OrderByDescending(c => c.CreatedAt).ToListAsync();
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
                var typeRefId = await _context.ReferenceEntries
                    .Where(r => r.IsActive &&
                        r.Value.ToLower() == t &&
                        (r.Category.ToLower() == "customer type" || r.Category.ToLower() == "customer_type"))
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                if (typeRefId == 0)
                    return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = $"Unknown customer type: {type}" };

                var list = await _context.Customers.Where(c => c.TypeId == typeRefId).OrderByDescending(c => c.CreatedAt).ToListAsync();
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
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(_context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = true, Data = new List<CustomerTimelineEntryDto>() };
                var rows = await _context.CustomerTimelines
                    .Where(x => x.CustomerCode == cc)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();
                var timelineCreatorNames = await LoadCreatorDisplayNamesAsync(rows.Select(x => (long?)x.CreatedBy));
                var dtos = rows.Select(x => new CustomerTimelineEntryDto
                {
                    Id = x.Id,
                    CustomerId = customerId,
                    Type = x.Type,
                    Notes = x.Notes,
                    FileId = x.FileId,
                    FileName = x.FileName,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    CreatedBy = x.CreatedBy,
                    CreatedByName = ResolveCreatorName(x.CreatedBy, timelineCreatorNames),
                    ModifiedAt = x.ModifiedAt,
                    ModifiedBy = x.ModifiedBy
                }).ToList();
                await EntityCodeResolution.EnrichCustomerTimelineDtosAsync(_context, customerId, dtos);
                return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = true, Data = dtos };
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
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(_context, 0, customerCode);
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
                var c = await _context.Customers.FindAsync(customerId);
                if (c == null)
                    return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = "Customer not found" };
                if (string.IsNullOrWhiteSpace(c.Code))
                    return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = "Customer has no code" };
                var now = DateTime.UtcNow;
                var e = new CustomerTimeline
                {
                    CustomerCode = c.Code,
                    Type = dto.Type,
                    Notes = dto.Notes ?? string.Empty,
                    FileId = dto.FileId,
                    FileName = dto.FileName,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.CustomerTimelines.Add(e);
                await _context.SaveChangesAsync();
                var entryNames = await LoadCreatorDisplayNamesAsync(new[] { (long?)e.CreatedBy });
                var entryDto = new CustomerTimelineEntryDto
                {
                    Id = e.Id,
                    CustomerId = customerId,
                    Type = e.Type,
                    Notes = e.Notes,
                    FileId = e.FileId,
                    FileName = e.FileName,
                    IsActive = e.IsActive,
                    CreatedAt = e.CreatedAt,
                    CreatedBy = e.CreatedBy,
                    CreatedByName = ResolveCreatorName(e.CreatedBy, entryNames),
                    ModifiedAt = e.ModifiedAt,
                    ModifiedBy = e.ModifiedBy
                };
                await EntityCodeResolution.EnrichCustomerTimelineDtosAsync(_context, customerId, new List<CustomerTimelineEntryDto> { entryDto });
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
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(_context, 0, customerCode);
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
