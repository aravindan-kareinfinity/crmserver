using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>InvestmentService.ts</c>.</summary>
    public interface IInvestmentService
    {
        Task<ApiResponse<List<InvestmentResponseDto>>> GetAll();
        Task<ApiResponse<InvestmentResponseDto>> GetInvestmentById(int id);
        Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerId(int customerId);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByStaffId(int staffId);
        Task<ApiResponse<InvestmentResponseDto>> CreateInvestment(CreateInvestmentDto dto);
        Task<ApiResponse<InvestmentResponseDto>> UpdateInvestment(int id, UpdateInvestmentDto dto);
        Task<ApiResponse<bool>> DeleteInvestment(int id);
        Task<ApiResponse<decimal>> GetTotalInvestmentByCustomer(int customerId);
        Task<ApiResponse<List<InvestmentTimelineEntryDto>>> GetTimeline(int investmentId);
        Task<ApiResponse<InvestmentTimelineEntryDto>> AddTimelineEntry(int investmentId, AddTimelineEntryDto dto);
    }

    public class InvestmentService : IInvestmentService
    {
        private readonly CrmDbContext _context;

        public InvestmentService(CrmDbContext context)
        {
            _context = context;
        }

        private static InvestmentResponseDto Map(Investment i) => new()
        {
            Id = i.Id,
            CustomerId = i.CustomerId,
            LocationId = i.LocationId,
            Amount = i.Amount,
            InvestmentTypeId = i.InvestmentTypeId,
            StaffId = i.StaffId,
            Notes = i.Notes,
            IsActive = i.IsActive,
            CreatedAt = i.CreatedAt,
            CreatedBy = i.CreatedBy,
            ModifiedAt = i.ModifiedAt
        };

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetAll()
        {
            try
            {
                var list = await _context.Investments.OrderByDescending(i => i.CreatedAt).ToListAsync();
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
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
                var i = await _context.Investments.FindAsync(id);
                if (i == null) return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = Map(i) };
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
                var q = _context.Investments.Where(i => i.CustomerId == customerId);
                var total = await q.CountAsync();
                var items = await q.OrderByDescending(i => i.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<InvestmentResponseDto>
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
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerId(int customerId)
        {
            try
            {
                var list = await _context.Investments.Where(i => i.CustomerId == customerId).OrderByDescending(i => i.CreatedAt).ToListAsync();
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByStaffId(int staffId)
        {
            try
            {
                var list = await _context.Investments.Where(i => i.StaffId == staffId).OrderByDescending(i => i.CreatedAt).ToListAsync();
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
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
                var inv = new Investment
                {
                    CustomerId = dto.CustomerId,
                    LocationId = dto.LocationId,
                    Amount = dto.Amount,
                    InvestmentTypeId = dto.InvestmentTypeId,
                    StaffId = dto.StaffId,
                    Notes = dto.Notes,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Investments.Add(inv);
                await _context.SaveChangesAsync();
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = Map(inv) };
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
                var i = await _context.Investments.FindAsync(id);
                if (i == null) return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };
                if (dto.CustomerId.HasValue) i.CustomerId = dto.CustomerId.Value;
                if (dto.LocationId.HasValue) i.LocationId = dto.LocationId.Value;
                if (dto.InvestmentTypeId.HasValue) i.InvestmentTypeId = dto.InvestmentTypeId.Value;
                if (dto.Amount.HasValue) i.Amount = dto.Amount.Value;
                if (dto.StaffIdCleared == true)
                    i.StaffId = null;
                else if (dto.StaffId.HasValue)
                    i.StaffId = dto.StaffId.Value;
                if (dto.Notes != null) i.Notes = dto.Notes;
                if (dto.IsActive.HasValue) i.IsActive = dto.IsActive.Value;
                i.ModifiedAt = DateTime.UtcNow;
                i.ModifiedBy = AuditUserIds.System;
                await _context.SaveChangesAsync();
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = Map(i) };
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
                var i = await _context.Investments.FindAsync(id);
                if (i == null) return new ApiResponse<bool> { Success = false, Message = "Investment not found" };
                _context.Investments.Remove(i);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
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
                var total = await _context.Investments.Where(i => i.CustomerId == customerId).SumAsync(i => i.Amount);
                return new ApiResponse<decimal> { Success = true, Data = total };
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
                var rows = await _context.InvestmentTimelines.Where(t => t.InvestmentId == investmentId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new InvestmentTimelineEntryDto
                    {
                        Id = t.Id,
                        InvestmentId = t.InvestmentId,
                        Type = t.Type,
                        Notes = t.Notes,
                        FileId = t.FileId,
                        FileName = t.FileName,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt,
                        CreatedBy = t.CreatedBy,
                        ModifiedAt = t.ModifiedAt,
                        ModifiedBy = t.ModifiedBy
                    }).ToListAsync();
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
                var parent = await _context.Investments.FindAsync(investmentId);
                if (parent == null) return new ApiResponse<InvestmentTimelineEntryDto> { Success = false, Message = "Investment not found" };
                var now = DateTime.UtcNow;
                var e = new InvestmentTimeline
                {
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
                };
                _context.InvestmentTimelines.Add(e);
                await _context.SaveChangesAsync();
                return new ApiResponse<InvestmentTimelineEntryDto>
                {
                    Success = true,
                    Data = new InvestmentTimelineEntryDto
                    {
                        Id = e.Id,
                        InvestmentId = e.InvestmentId,
                        Type = e.Type,
                        Notes = e.Notes,
                        FileId = e.FileId,
                        FileName = e.FileName,
                        IsActive = e.IsActive,
                        CreatedAt = e.CreatedAt,
                        CreatedBy = e.CreatedBy,
                        ModifiedAt = e.ModifiedAt,
                        ModifiedBy = e.ModifiedBy
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentTimelineEntryDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
