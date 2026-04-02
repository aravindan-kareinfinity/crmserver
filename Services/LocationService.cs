using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>BranchService.ts</c> → <c>LocationService</c>.</summary>
    public interface ILocationService
    {
        Task<ApiResponse<List<LocationResponseDto>>> GetAll();
        Task<ApiResponse<LocationResponseDto>> GetById(int id);
        Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomerCode(string customerCode, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerId(int customerId);
        Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerCode(string customerCode);
        Task<ApiResponse<LocationResponseDto>> CreateLocation(CreateLocationDto dto);
        Task<ApiResponse<LocationResponseDto>> UpdateLocation(int id, CreateLocationDto dto);
        Task<ApiResponse<bool>> DeleteLocation(int id);
        Task<ApiResponse<List<LocationTimelineEntryDto>>> GetTimeline(int locationId);
    }

    public class LocationService : ILocationService
    {
        private readonly CrmDbContext _context;

        public LocationService(CrmDbContext context)
        {
            _context = context;
        }

        private static LocationResponseDto Map(Location b, int customerId) => new()
        {
            Id = b.Id,
            CustomerId = customerId,
            CustomerCode = b.CustomerCode,
            Code = b.Code,
            Name = b.Name,
            RegName = b.RegName,
            Pincode = b.Pincode,
            CityId = b.CityId,
            StateId = b.StateId,
            CountryId = b.CountryId,
            AddressLine1 = b.AddressLine1,
            AddressLine2 = b.AddressLine2,
            ContactPersons = b.ContactPersons,
            Emails = b.Emails,
            Mobiles = b.Mobiles,
            ShopSizeId = b.ShopSizeId,
            TierId = b.TierId,
            IsPrimary = b.IsPrimary,
            GstNumber = b.GstNumber,
            IsActive = b.IsActive,
            CreatedAt = b.CreatedAt,
            CreatedBy = b.CreatedBy,
            ModifiedAt = b.ModifiedAt,
            ModifiedBy = b.ModifiedBy
        };

        public async Task<ApiResponse<List<LocationResponseDto>>> GetAll()
        {
            try
            {
                var rows = await _context.Locations.AsNoTracking()
                    .Join(
                        _context.Customers.AsNoTracking(),
                        l => l.CustomerCode,
                        c => c.Code,
                        (l, c) => new { Location = l, CustomerId = c.Id }
                    )
                    .OrderByDescending(x => x.Location.CreatedAt)
                    .ToListAsync();
                var dtos = rows.Select(x => Map(x.Location, x.CustomerId)).ToList();
                return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<LocationResponseDto>> GetById(int id)
        {
            try
            {
                var row = await _context.Locations.AsNoTracking()
                    .Where(l => l.Id == id)
                    .Join(
                        _context.Customers.AsNoTracking(),
                        l => l.CustomerCode,
                        c => c.Code,
                        (l, c) => new { Location = l, CustomerId = c.Id }
                    )
                    .FirstOrDefaultAsync();
                if (row == null) return new ApiResponse<LocationResponseDto> { Success = false, Message = "Location not found" };
                return new ApiResponse<LocationResponseDto> { Success = true, Data = Map(row.Location, row.CustomerId) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LocationResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(_context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<PaginatedResponse<LocationResponseDto>>
                    {
                        Success = true,
                        Data = new PaginatedResponse<LocationResponseDto>
                        {
                            Items = new List<LocationResponseDto>(),
                            Total = 0,
                            PageNumber = pageNumber,
                            PageSize = pageSize
                        }
                    };
                var q = _context.Locations.AsNoTracking().Where(b => b.CustomerCode == cc);
                var total = await q.CountAsync();
                var items = await q.OrderByDescending(b => b.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                var dtos = items.Select(x => Map(x, customerId)).ToList();
                return new ApiResponse<PaginatedResponse<LocationResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<LocationResponseDto>
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
                return new ApiResponse<PaginatedResponse<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomerCode(
            string customerCode,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(_context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<PaginatedResponse<LocationResponseDto>> { Success = false, Message = err };
                return await GetLocationsByCustomer(cid, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerId(int customerId)
        {
            try
            {
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(_context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = new List<LocationResponseDto>() };
                var list = await _context.Locations.AsNoTracking()
                    .Where(b => b.CustomerCode == cc)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();
                var dtos = list.Select(x => Map(x, customerId)).ToList();
                return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerCode(string customerCode)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(_context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = err };
                return await GetByCustomerId(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<LocationResponseDto>> CreateLocation(CreateLocationDto dto)
        {
            try
            {
                var (custCode, custId, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(_context, dto.CustomerId, dto.CustomerCode);
                if (cErr != null)
                    return new ApiResponse<LocationResponseDto> { Success = false, Message = cErr };

                var now = DateTime.UtcNow;
                var code = string.IsNullOrWhiteSpace(dto.Code)
                    ? $"LOC-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}"
                    : dto.Code;
                var location = new Location
                {
                    CustomerCode = custCode,
                    Name = dto.Name,
                    RegName = dto.RegName,
                    Code = code,
                    Pincode = dto.Pincode,
                    CityId = dto.CityId,
                    StateId = dto.StateId,
                    CountryId = dto.CountryId,
                    AddressLine1 = dto.AddressLine1,
                    AddressLine2 = dto.AddressLine2,
                    ContactPersons = dto.ContactPersons,
                    Emails = dto.Emails,
                    Mobiles = dto.Mobiles,
                    ShopSizeId = dto.ShopSizeId,
                    TierId = dto.TierId,
                    IsPrimary = dto.IsPrimary,
                    GstNumber = dto.GstNumber,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Locations.Add(location);
                await _context.SaveChangesAsync();
                var created = Map(location, custId);
                return new ApiResponse<LocationResponseDto> { Success = true, Data = created };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LocationResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<LocationResponseDto>> UpdateLocation(int id, CreateLocationDto dto)
        {
            try
            {
                var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
                if (location == null) return new ApiResponse<LocationResponseDto> { Success = false, Message = "Location not found" };
                int customerId = 0;

                if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                {
                    var (cc, cid, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(_context, 0, dto.CustomerCode);
                    if (cErr != null)
                        return new ApiResponse<LocationResponseDto> { Success = false, Message = cErr };
                    location.CustomerCode = cc;
                    customerId = cid;
                }
                else if (dto.CustomerId > 0)
                {
                    var (cc, cid, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(_context, dto.CustomerId, null);
                    if (cErr != null)
                        return new ApiResponse<LocationResponseDto> { Success = false, Message = cErr };
                    location.CustomerCode = cc;
                    customerId = cid;
                }
                else
                {
                    var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(_context, 0, location.CustomerCode);
                    if (err == null) customerId = cid;
                }

                location.Name = dto.Name;
                location.RegName = dto.RegName;
                if (!string.IsNullOrWhiteSpace(dto.Code)) location.Code = dto.Code;
                location.AddressLine1 = dto.AddressLine1;
                location.AddressLine2 = dto.AddressLine2;
                location.Pincode = dto.Pincode;
                location.GstNumber = dto.GstNumber;
                location.CityId = dto.CityId;
                location.StateId = dto.StateId;
                location.CountryId = dto.CountryId;
                location.ContactPersons = dto.ContactPersons;
                location.Emails = dto.Emails;
                location.Mobiles = dto.Mobiles;
                location.ShopSizeId = dto.ShopSizeId;
                location.TierId = dto.TierId;
                location.IsPrimary = dto.IsPrimary;
                location.ModifiedAt = DateTime.UtcNow;
                location.ModifiedBy = AuditUserIds.System;
                await _context.SaveChangesAsync();
                var updated = Map(location, customerId);
                return new ApiResponse<LocationResponseDto> { Success = true, Data = updated };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LocationResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteLocation(int id)
        {
            try
            {
                var x = await _context.Locations.FindAsync(id);
                if (x == null) return new ApiResponse<bool> { Success = false, Message = "Location not found" };
                _context.Locations.Remove(x);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<LocationTimelineEntryDto>>> GetTimeline(int locationId)
        {
            try
            {
                var rows = await _context.LocationTimelines
                    .Where(t => t.LocationId == locationId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new LocationTimelineEntryDto
                    {
                        Id = t.Id,
                        LocationId = t.LocationId,
                        Type = t.Type,
                        Notes = t.Notes,
                        FileId = t.FileId,
                        FileName = t.FileName,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt,
                        CreatedBy = t.CreatedBy,
                        ModifiedAt = t.ModifiedAt,
                        ModifiedBy = t.ModifiedBy
                    })
                    .ToListAsync();
                return new ApiResponse<List<LocationTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }
    }
}
