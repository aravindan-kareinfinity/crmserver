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
        Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerId(int customerId);
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

        private static LocationResponseDto Map(Location b) => new()
        {
            Id = b.Id,
            CustomerId = b.CustomerId,
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
                var list = await _context.Locations.OrderByDescending(x => x.CreatedAt).ToListAsync();
                return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
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
                var x = await _context.Locations.FindAsync(id);
                if (x == null) return new ApiResponse<LocationResponseDto> { Success = false, Message = "Location not found" };
                return new ApiResponse<LocationResponseDto> { Success = true, Data = Map(x) };
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
                var q = _context.Locations.Where(b => b.CustomerId == customerId);
                var total = await q.CountAsync();
                var items = await q.OrderByDescending(b => b.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                return new ApiResponse<PaginatedResponse<LocationResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<LocationResponseDto>
                    {
                        Items = items.Select(Map).ToList(),
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

        public async Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerId(int customerId)
        {
            try
            {
                var list = await _context.Locations.Where(b => b.CustomerId == customerId).OrderByDescending(b => b.CreatedAt).ToListAsync();
                return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
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
                var now = DateTime.UtcNow;
                var code = string.IsNullOrWhiteSpace(dto.Code)
                    ? $"LOC-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}"
                    : dto.Code;
                var location = new Location
                {
                    CustomerId = dto.CustomerId,
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
                return new ApiResponse<LocationResponseDto> { Success = true, Data = Map(location) };
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
                var location = await _context.Locations.FindAsync(id);
                if (location == null) return new ApiResponse<LocationResponseDto> { Success = false, Message = "Location not found" };
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
                return new ApiResponse<LocationResponseDto> { Success = true, Data = Map(location) };
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
