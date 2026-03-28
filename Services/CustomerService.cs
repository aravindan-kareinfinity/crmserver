using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>CustomerService.ts</c> (getAll, getById, getByType, CRUD, getTimeline).</summary>
    public interface ICustomerService
    {
        Task<ApiResponse<PaginatedResponse<CustomerResponseDto>>> GetAllCustomers(int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
        Task<ApiResponse<List<CustomerResponseDto>>> GetAllCustomersList();
        Task<ApiResponse<CustomerResponseDto>> GetCustomerById(int id);
        Task<ApiResponse<CustomerResponseDto>> CreateCustomer(CreateCustomerDto dto);
        Task<ApiResponse<CustomerResponseDto>> UpdateCustomer(int id, UpdateCustomerDto dto);
        Task<ApiResponse<bool>> DeleteCustomer(int id);
        Task<ApiResponse<List<CustomerResponseDto>>> GetCustomersByTypeId(int typeId);
        /// <summary>UI <c>getByType('lead' | 'prospect' | 'customer')</c> — resolves reference_entries (category "Customer Type").</summary>
        Task<ApiResponse<List<CustomerResponseDto>>> GetCustomersByType(string type);
        Task<ApiResponse<List<CustomerTimelineEntryDto>>> GetCustomerTimeline(int customerId);
        Task<ApiResponse<CustomerTimelineEntryDto>> AddCustomerTimelineEntry(int customerId, AddTimelineEntryDto dto);
        /// <summary>Parse .xlsx (first sheet), validate like SPA bulk rules, insert all in one transaction or return row errors.</summary>
        Task<ApiResponse<BulkImportCustomersResultDto>> ImportCustomersFromSpreadsheetAsync(Stream stream);
    }

    public partial class CustomerService : ICustomerService
    {
        private readonly CrmDbContext _context;

        public CustomerService(CrmDbContext context)
        {
            _context = context;
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
            return names.TryGetValue(createdBy.Value, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;
        }

        private static string? ResolveCreatorName(long createdBy, IReadOnlyDictionary<long, string> names) =>
            names.TryGetValue(createdBy, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;

        /// <summary>Second key for <c>pg_advisory_xact_lock</c> (customer code sequence).</summary>
        private const int CustomerCodeAdvisoryLockKey2 = 0x4372_6D63;

        private async Task<int> GetMaxCustomerSequenceForYearAsync(int year)
        {
            var prefix = $"{year}/";
            var codes = await _context.Customers.AsNoTracking()
                .Where(c => c.Code != null && c.Code.StartsWith(prefix))
                .Select(c => c.Code!)
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

        private static CustomerResponseDto MapCustomer(Customer c, string? createdByName = null) => new()
        {
            Id = c.Id,
            Code = c.Code,
            RegName = c.RegName,
            Mobile = c.Mobile,
            Email = c.Email,
            BusinessTypeId = c.BusinessTypeId,
            IndustryId = c.IndustryId,
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
            CreatedByName = createdByName,
            ModifiedAt = c.ModifiedAt,
            ModifiedBy = c.ModifiedBy,
            ConvertedAt = c.ConvertedAt,
            ConvertedBy = c.ConvertedBy,
            PipelineStatus = c.PipelineStatus
        };

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
                        (c.Code != null && c.Code.Contains(searchTerm)));
                }

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var creatorNames = await LoadCreatorDisplayNamesAsync(items.Select(c => c.CreatedBy));

                return new ApiResponse<PaginatedResponse<CustomerResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<CustomerResponseDto>
                    {
                        Items = items.Select(c => MapCustomer(c, ResolveCreatorName(c.CreatedBy, creatorNames))).ToList(),
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<CustomerResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<CustomerResponseDto>>> GetAllCustomersList()
        {
            try
            {
                var list = await _context.Customers.OrderByDescending(c => c.CreatedAt).ToListAsync();
                var creatorNames = await LoadCreatorDisplayNamesAsync(list.Select(c => c.CreatedBy));
                return new ApiResponse<List<CustomerResponseDto>>
                {
                    Success = true,
                    Data = list.Select(c => MapCustomer(c, ResolveCreatorName(c.CreatedBy, creatorNames))).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> GetCustomerById(int id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                    return new ApiResponse<CustomerResponseDto> { Success = false, Message = "Customer not found" };
                var creatorNames = await LoadCreatorDisplayNamesAsync(new[] { customer.CreatedBy });
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = true,
                    Data = MapCustomer(customer, ResolveCreatorName(customer.CreatedBy, creatorNames))
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> CreateCustomer(CreateCustomerDto dto)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var year = now.Year;
                var codes = await ReserveCustomerCodesAsync(year, 1);
                var customer = new Customer
                {
                    Code = codes[0],
                    RegName = dto.RegName,
                    Mobile = dto.Mobile,
                    Email = dto.Email,
                    BusinessTypeId = dto.BusinessTypeId,
                    IndustryId = dto.IndustryId,
                    AddressLine1 = dto.AddressLine1,
                    AddressLine2 = dto.AddressLine2,
                    CityId = dto.CityId,
                    StateId = dto.StateId,
                    CountryId = dto.CountryId,
                    Pincode = dto.Pincode,
                    GstNumber = dto.GstNumber,
                    ContactPersons = dto.ContactPersons,
                    Emails = dto.Emails,
                    Mobiles = dto.Mobiles,
                    ShopSizeId = dto.ShopSizeId,
                    TierId = dto.TierId,
                    TypeId = dto.TypeId,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                var createdNames = await LoadCreatorDisplayNamesAsync(new[] { customer.CreatedBy });
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = true,
                    Message = "Created",
                    Data = MapCustomer(customer, ResolveCreatorName(customer.CreatedBy, createdNames))
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<CustomerResponseDto>> UpdateCustomer(int id, UpdateCustomerDto dto)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                    return new ApiResponse<CustomerResponseDto> { Success = false, Message = "Customer not found" };

                if (!string.IsNullOrWhiteSpace(dto.RegName)) customer.RegName = dto.RegName;
                if (!string.IsNullOrWhiteSpace(dto.Mobile)) customer.Mobile = dto.Mobile;
                if (!string.IsNullOrWhiteSpace(dto.Email)) customer.Email = dto.Email;
                if (dto.BusinessTypeId.HasValue) customer.BusinessTypeId = dto.BusinessTypeId;
                if (dto.IndustryId.HasValue) customer.IndustryId = dto.IndustryId;
                if (!string.IsNullOrWhiteSpace(dto.AddressLine1)) customer.AddressLine1 = dto.AddressLine1;
                if (dto.AddressLine2 != null) customer.AddressLine2 = dto.AddressLine2;
                if (dto.CityId.HasValue) customer.CityId = dto.CityId;
                if (dto.StateId.HasValue) customer.StateId = dto.StateId;
                if (dto.CountryId.HasValue) customer.CountryId = dto.CountryId;
                if (!string.IsNullOrWhiteSpace(dto.Pincode)) customer.Pincode = dto.Pincode;
                if (dto.GstNumber != null) customer.GstNumber = dto.GstNumber;
                if (dto.ShopSizeId.HasValue) customer.ShopSizeId = dto.ShopSizeId.Value;
                if (dto.TierId.HasValue) customer.TierId = dto.TierId.Value;
                if (dto.ContactPersons != null) customer.ContactPersons = dto.ContactPersons;
                if (dto.Emails != null) customer.Emails = dto.Emails;
                if (dto.Mobiles != null) customer.Mobiles = dto.Mobiles;
                if (dto.TypeId.HasValue) customer.TypeId = dto.TypeId.Value;
                if (dto.IsActive.HasValue) customer.IsActive = dto.IsActive.Value;
                if (dto.ConvertedAt.HasValue) customer.ConvertedAt = dto.ConvertedAt;
                if (dto.ConvertedBy != null) customer.ConvertedBy = dto.ConvertedBy;
                if (dto.PipelineStatus != null)
                    customer.PipelineStatus = string.IsNullOrWhiteSpace(dto.PipelineStatus)
                        ? null
                        : dto.PipelineStatus.Trim();

                customer.ModifiedAt = DateTime.UtcNow;
                customer.ModifiedBy = AuditUserIds.System;
                await _context.SaveChangesAsync();
                var updatedNames = await LoadCreatorDisplayNamesAsync(new[] { customer.CreatedBy });
                return new ApiResponse<CustomerResponseDto>
                {
                    Success = true,
                    Data = MapCustomer(customer, ResolveCreatorName(customer.CreatedBy, updatedNames))
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerResponseDto> { Success = false, Message = ex.Message };
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
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<CustomerResponseDto>>> GetCustomersByTypeId(int typeId)
        {
            try
            {
                var list = await _context.Customers.Where(c => c.TypeId == typeId).OrderByDescending(c => c.CreatedAt).ToListAsync();
                var creatorNames = await LoadCreatorDisplayNamesAsync(list.Select(c => c.CreatedBy));
                return new ApiResponse<List<CustomerResponseDto>>
                {
                    Success = true,
                    Data = list.Select(c => MapCustomer(c, ResolveCreatorName(c.CreatedBy, creatorNames))).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = ex.Message };
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
                var creatorNames = await LoadCreatorDisplayNamesAsync(list.Select(c => c.CreatedBy));
                return new ApiResponse<List<CustomerResponseDto>>
                {
                    Success = true,
                    Data = list.Select(c => MapCustomer(c, ResolveCreatorName(c.CreatedBy, creatorNames))).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<CustomerTimelineEntryDto>>> GetCustomerTimeline(int customerId)
        {
            try
            {
                var rows = await _context.CustomerTimelines
                    .Where(x => x.CustomerId == customerId)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();
                var timelineCreatorNames = await LoadCreatorDisplayNamesAsync(rows.Select(x => (long?)x.CreatedBy));
                var dtos = rows.Select(x => new CustomerTimelineEntryDto
                {
                    Id = x.Id,
                    CustomerId = x.CustomerId,
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
                return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<CustomerTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<CustomerTimelineEntryDto>> AddCustomerTimelineEntry(int customerId, AddTimelineEntryDto dto)
        {
            try
            {
                var c = await _context.Customers.FindAsync(customerId);
                if (c == null)
                    return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = "Customer not found" };
                var now = DateTime.UtcNow;
                var e = new CustomerTimeline
                {
                    CustomerId = customerId,
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
                return new ApiResponse<CustomerTimelineEntryDto>
                {
                    Success = true,
                    Data = new CustomerTimelineEntryDto
                    {
                        Id = e.Id,
                        CustomerId = e.CustomerId,
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
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<CustomerTimelineEntryDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
