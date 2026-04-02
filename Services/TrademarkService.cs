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
        Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomerCode(string customerCode, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<TrademarkResponseDto>> GetTrademarkById(int id);
        Task<ApiResponse<List<TrademarkResponseDto>>> GetTrademarksByActive(bool isActive);
        Task<ApiResponse<TrademarkResponseDto>> CreateTrademark(CreateTrademarkDto dto);
        Task<ApiResponse<TrademarkResponseDto>> UpdateTrademark(int id, UpdateTrademarkDto dto);
        Task<ApiResponse<bool>> DeleteTrademark(int id);
    }

    public class TrademarkService : ITrademarkService
    {
        CrmDbContext context;

        public TrademarkService(CrmDbContext context)
        {
            this.context = context;
        }

        private static TrademarkResponseDto Map(Trademark t) => new()
        {
            Id = t.Id,
            CustomerId = t.Customer?.Id ?? 0,
            CustomerCode = t.CustomerCode,
            LocationId = t.LocationId,
            LocationCode = t.Location?.Code,
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
                var list = await context.Trademarks
                    .AsNoTracking()
                    .Include(t => t.Customer)
                    .Include(t => t.Location)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
                var dtos = list.Select(Map).ToList();
                return new ApiResponse<List<TrademarkResponseDto>> { Success = true, Data = dtos };
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
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<PaginatedResponse<TrademarkResponseDto>>
                    {
                        Success = true,
                        Data = new PaginatedResponse<TrademarkResponseDto>
                        {
                            Items = new List<TrademarkResponseDto>(),
                            Total = 0,
                            PageNumber = pageNumber,
                            PageSize = pageSize
                        }
                    };
                var q = context.Trademarks.Include(t => t.Customer).Include(t => t.Location).Where(t => t.CustomerCode == cc);
                var total = await q.CountAsync();
                var items = await q.OrderByDescending(t => t.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                var dtos = items.Select(Map).ToList();
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<TrademarkResponseDto>
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
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> GetTrademarkById(int id)
        {
            try
            {
                var t = await context.Trademarks
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.Location)
                    .FirstOrDefaultAsync(x => x.Id == id);
                if (t == null) return new ApiResponse<TrademarkResponseDto> { Success = false, Message = "Trademark not found" };
                var one = Map(t);
                return new ApiResponse<TrademarkResponseDto> { Success = true, Data = one };
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
                var list = await context.Trademarks
                    .Include(t => t.Customer)
                    .Include(t => t.Location)
                    .Where(t => t.IsActive == isActive)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
                var dtos = list.Select(Map).ToList();
                return new ApiResponse<List<TrademarkResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomerCode(
            string customerCode,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<PaginatedResponse<TrademarkResponseDto>> { Success = false, Message = err };
                return await GetTrademarksByCustomer(cid, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> CreateTrademark(CreateTrademarkDto dto)
        {
            try
            {
                var (custCode, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, dto.CustomerId, dto.CustomerCode);
                if (cErr != null)
                    return new ApiResponse<TrademarkResponseDto> { Success = false, Message = cErr };
                var (locationId, lErr) = await EntityCodeResolution.ResolveRequiredLocationIdAsync(
                    context, custCode, dto.LocationId, dto.LocationCode);
                if (lErr != null)
                    return new ApiResponse<TrademarkResponseDto> { Success = false, Message = lErr };

                var now = DateTime.UtcNow;
                var t = new Trademark
                {
                    CustomerCode = custCode,
                    LocationId = locationId,
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
                context.Trademarks.Add(t);
                await context.SaveChangesAsync();
                await context.Entry(t).Reference(x => x.Customer).LoadAsync();
                await context.Entry(t).Reference(x => x.Location).LoadAsync();
                var created = Map(t);
                return new ApiResponse<TrademarkResponseDto> { Success = true, Data = created };
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
                var t = await context.Trademarks.Include(x => x.Customer).Include(x => x.Location).FirstOrDefaultAsync(x => x.Id == id);
                if (t == null) return new ApiResponse<TrademarkResponseDto> { Success = false, Message = "Trademark not found" };

                if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                {
                    var (cc, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, 0, dto.CustomerCode);
                    if (cErr != null)
                        return new ApiResponse<TrademarkResponseDto> { Success = false, Message = cErr };
                    t.CustomerCode = cc;
                }
                else
                {
                    var (cc, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, dto.CustomerId, null);
                    if (cErr != null)
                        return new ApiResponse<TrademarkResponseDto> { Success = false, Message = cErr };
                    t.CustomerCode = cc;
                }

                if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                {
                    var (lid, lErr) = await EntityCodeResolution.ResolveRequiredLocationIdAsync(
                        context, t.CustomerCode, 0, dto.LocationCode);
                    if (lErr != null)
                        return new ApiResponse<TrademarkResponseDto> { Success = false, Message = lErr };
                    t.LocationId = lid;
                }
                else
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
                await context.SaveChangesAsync();
                await context.Entry(t).Reference(x => x.Customer).LoadAsync();
                await context.Entry(t).Reference(x => x.Location).LoadAsync();
                var updated = Map(t);
                return new ApiResponse<TrademarkResponseDto> { Success = true, Data = updated };
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
                var t = await context.Trademarks.FindAsync(id);
                if (t == null) return new ApiResponse<bool> { Success = false, Message = "Trademark not found" };
                context.Trademarks.Remove(t);
                await context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
