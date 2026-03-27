using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public interface ITrademarkService
    {
        Task<ApiResponse<List<TrademarkResponseDto>>> GetAll();
        Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomer(int customerId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<TrademarkResponseDto>> GetTrademarkById(int id);
        Task<ApiResponse<List<TrademarkResponseDto>>> GetTrademarksByActive(bool isActive);
        Task<ApiResponse<TrademarkResponseDto>> CreateTrademark(CreateTrademarkDto dto);
        Task<ApiResponse<TrademarkResponseDto>> UpdateTrademark(int id, UpdateTrademarkDto dto);
        Task<ApiResponse<bool>> DeleteTrademark(int id);
    }

    public class TrademarkService : ITrademarkService
    {
        private readonly CrmDbContext _context;

        public TrademarkService(CrmDbContext context)
        {
            _context = context;
        }

        private static TrademarkResponseDto Map(Trademark t) => new()
        {
            Id = t.Id,
            CustomerId = t.CustomerId,
            LocationId = t.LocationId,
            RegName = t.RegName,
            GstNumber = t.GstNumber,
            Pincode = t.Pincode,
            CityId = t.CityId,
            StateId = t.StateId,
            CountryId = t.CountryId,
            AddressLine1 = t.AddressLine1,
            AddressLine2 = t.AddressLine2,
            ContactPersons = t.ContactPersons,
            Emails = t.Emails,
            Mobiles = t.Mobiles,
            TierId = t.TierId,
            ShopSizeId = t.ShopSizeId,
            RegistrationNumber = t.RegistrationNumber,
            Category = t.Category,
            Description = t.Description,
            RegistrationDate = t.RegistrationDate,
            ExpiryDate = t.ExpiryDate,
            IsActive = t.IsActive,
            Remarks = t.Remarks,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            ModifiedAt = t.ModifiedAt,
            ModifiedBy = t.ModifiedBy
        };

        public async Task<ApiResponse<List<TrademarkResponseDto>>> GetAll()
        {
            try
            {
                var list = await _context.Trademarks.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync();
                return new ApiResponse<List<TrademarkResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomer(int customerId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var q = _context.Trademarks.Where(t => t.CustomerId == customerId);
                var total = await q.CountAsync();
                var items = await q.OrderByDescending(t => t.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<TrademarkResponseDto>
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
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> GetTrademarkById(int id)
        {
            try
            {
                var t = await _context.Trademarks.FindAsync(id);
                if (t == null) return new ApiResponse<TrademarkResponseDto> { Success = false, Message = "Trademark not found" };
                return new ApiResponse<TrademarkResponseDto> { Success = true, Data = Map(t) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TrademarkResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TrademarkResponseDto>>> GetTrademarksByActive(bool isActive)
        {
            try
            {
                var list = await _context.Trademarks.Where(t => t.IsActive == isActive).OrderByDescending(t => t.CreatedAt).ToListAsync();
                return new ApiResponse<List<TrademarkResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> CreateTrademark(CreateTrademarkDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                var t = new Trademark
                {
                    CustomerId = dto.CustomerId,
                    LocationId = dto.LocationId,
                    RegName = dto.RegName,
                    GstNumber = dto.GstNumber,
                    Pincode = dto.Pincode,
                    CityId = dto.CityId,
                    StateId = dto.StateId,
                    CountryId = dto.CountryId,
                    AddressLine1 = dto.AddressLine1,
                    AddressLine2 = dto.AddressLine2,
                    ContactPersons = dto.ContactPersons,
                    Emails = dto.Emails,
                    Mobiles = dto.Mobiles,
                    TierId = dto.TierId,
                    ShopSizeId = dto.ShopSizeId,
                    RegistrationNumber = dto.RegistrationNumber,
                    Category = dto.Category,
                    Description = dto.Description,
                    RegistrationDate = dto.RegistrationDate,
                    ExpiryDate = dto.ExpiryDate,
                    IsActive = dto.IsActive,
                    Remarks = dto.Remarks,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Trademarks.Add(t);
                await _context.SaveChangesAsync();
                return new ApiResponse<TrademarkResponseDto> { Success = true, Data = Map(t) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TrademarkResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> UpdateTrademark(int id, UpdateTrademarkDto dto)
        {
            try
            {
                var t = await _context.Trademarks.FindAsync(id);
                if (t == null) return new ApiResponse<TrademarkResponseDto> { Success = false, Message = "Trademark not found" };
                t.CustomerId = dto.CustomerId;
                t.LocationId = dto.LocationId;
                t.RegName = dto.RegName;
                t.GstNumber = dto.GstNumber;
                t.Pincode = dto.Pincode;
                t.CityId = dto.CityId;
                t.StateId = dto.StateId;
                t.CountryId = dto.CountryId;
                t.AddressLine1 = dto.AddressLine1;
                t.AddressLine2 = dto.AddressLine2;
                t.ContactPersons = dto.ContactPersons;
                t.Emails = dto.Emails;
                t.Mobiles = dto.Mobiles;
                t.TierId = dto.TierId;
                t.ShopSizeId = dto.ShopSizeId;
                t.RegistrationNumber = dto.RegistrationNumber;
                t.Category = dto.Category;
                t.Description = dto.Description;
                t.RegistrationDate = dto.RegistrationDate;
                t.ExpiryDate = dto.ExpiryDate;
                t.IsActive = dto.IsActive;
                t.Remarks = dto.Remarks;
                t.ModifiedAt = DateTime.UtcNow;
                t.ModifiedBy = AuditUserIds.System;
                await _context.SaveChangesAsync();
                return new ApiResponse<TrademarkResponseDto> { Success = true, Data = Map(t) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TrademarkResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteTrademark(int id)
        {
            try
            {
                var t = await _context.Trademarks.FindAsync(id);
                if (t == null) return new ApiResponse<bool> { Success = false, Message = "Trademark not found" };
                _context.Trademarks.Remove(t);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
