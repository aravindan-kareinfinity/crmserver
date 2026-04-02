using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>ReferenceService.ts</c>.</summary>
    public interface IReferenceService
    {
        Task<ApiResponse<List<ReferenceResponseDto>>> GetAll();
        Task<ApiResponse<List<ReferenceResponseDto>>> GetReferencesByCategory(string category);
        Task<ApiResponse<ReferenceResponseDto>> GetReferenceById(int id);
        Task<ApiResponse<ReferenceResponseDto>> GetByValue(string value);
        Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelById(int id);
        Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelByValue(string value);
        Task<ApiResponse<ReferenceResponseDto>> Create(CreateReferenceDto dto);
        Task<ApiResponse<ReferenceResponseDto>> Update(int id, UpdateReferenceDto dto);
        Task<ApiResponse<bool>> Delete(int id);
    }

    public class ReferenceService : IReferenceService
    {
        CrmDbContext context;

        public ReferenceService(CrmDbContext context)
        {
            this.context = context;
        }

        private static ReferenceResponseDto Map(ReferenceEntry r) => new()
        {
            Id = r.Id,
            Category = r.Category,
            Label = r.Label,
            Value = r.Value,
            IsActive = r.IsActive,
            SortOrder = r.SortOrder,
            RequiresImplementation = r.RequiresImplementation,
            IsImplementation = r.IsImplementation
        };

        public async Task<ApiResponse<List<ReferenceResponseDto>>> GetAll()
        {
            try
            {
                var list = await context.ReferenceEntries.OrderBy(r => r.Category).ThenBy(r => r.SortOrder).ToListAsync();
                return new ApiResponse<List<ReferenceResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ReferenceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<ReferenceResponseDto>>> GetReferencesByCategory(string category)
        {
            try
            {
                var list = await context.ReferenceEntries
                    .Where(r => r.Category == category && r.IsActive)
                    .OrderBy(r => r.SortOrder)
                    .ToListAsync();
                return new ApiResponse<List<ReferenceResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ReferenceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> GetReferenceById(int id)
        {
            try
            {
                var r = await context.ReferenceEntries.FindAsync(id);
                if (r == null) return new ApiResponse<ReferenceResponseDto> { Success = false, Message = "Reference not found" };
                return new ApiResponse<ReferenceResponseDto> { Success = true, Data = Map(r) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> GetByValue(string value)
        {
            try
            {
                var r = await context.ReferenceEntries.FirstOrDefaultAsync(x => x.Value == value);
                if (r == null) return new ApiResponse<ReferenceResponseDto> { Success = false, Message = "Reference not found" };
                return new ApiResponse<ReferenceResponseDto> { Success = true, Data = Map(r) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelById(int id)
        {
            try
            {
                var r = await context.ReferenceEntries.FindAsync(id);
                return new ApiResponse<ReferenceLabelResponseDto>
                {
                    Success = true,
                    Data = new ReferenceLabelResponseDto { Label = r?.Label ?? id.ToString() }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceLabelResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelByValue(string value)
        {
            try
            {
                var r = await context.ReferenceEntries.FirstOrDefaultAsync(x => x.Value == value);
                return new ApiResponse<ReferenceLabelResponseDto>
                {
                    Success = true,
                    Data = new ReferenceLabelResponseDto { Label = r?.Label ?? value }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceLabelResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> Create(CreateReferenceDto dto)
        {
            try
            {
                var e = new ReferenceEntry
                {
                    Category = dto.Category.Trim(),
                    Label = dto.Label.Trim(),
                    Value = dto.Value.Trim(),
                    IsActive = dto.IsActive,
                    SortOrder = dto.SortOrder,
                    RequiresImplementation = dto.RequiresImplementation,
                    IsImplementation = dto.IsImplementation
                };
                context.ReferenceEntries.Add(e);
                await context.SaveChangesAsync();
                return new ApiResponse<ReferenceResponseDto> { Success = true, Data = Map(e) };
            }
            catch (DbUpdateException ex)
            {
                return new ApiResponse<ReferenceResponseDto>
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> Update(int id, UpdateReferenceDto dto)
        {
            try
            {
                var r = await context.ReferenceEntries.FindAsync(id);
                if (r == null) return new ApiResponse<ReferenceResponseDto> { Success = false, Message = "Reference not found" };
                r.Category = dto.Category.Trim();
                r.Label = dto.Label.Trim();
                r.Value = dto.Value.Trim();
                r.IsActive = dto.IsActive;
                r.SortOrder = dto.SortOrder;
                r.RequiresImplementation = dto.RequiresImplementation;
                r.IsImplementation = dto.IsImplementation;
                await context.SaveChangesAsync();
                return new ApiResponse<ReferenceResponseDto> { Success = true, Data = Map(r) };
            }
            catch (DbUpdateException ex)
            {
                return new ApiResponse<ReferenceResponseDto>
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> Delete(int id)
        {
            try
            {
                var r = await context.ReferenceEntries.FindAsync(id);
                if (r == null) return new ApiResponse<bool> { Success = false, Message = "Reference not found" };
                context.ReferenceEntries.Remove(r);
                await context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (DbUpdateException ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
