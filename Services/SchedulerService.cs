using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public interface ISchedulerService
    {
        Task<ApiResponse<List<SchedulerEventResponseDto>>> GetAll();
        Task<ApiResponse<SchedulerEventResponseDto>> GetById(int id);
        Task<ApiResponse<SchedulerEventResponseDto>> Create(CreateSchedulerEventDto dto);
        Task<ApiResponse<SchedulerEventResponseDto>> Update(int id, UpdateSchedulerEventDto dto);
        Task<ApiResponse<bool>> Delete(int id);
    }

    public class SchedulerService : ISchedulerService
    {
        CrmDbContext context;

        public SchedulerService(CrmDbContext context)
        {
            this.context = context;
        }

        private static bool IsActiveForStatus(string status) =>
            !string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase);

        private static SchedulerEventResponseDto Map(SchedulerEvent e) => new()
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Attendees = e.Attendees ?? new List<int>(),
            Location = e.Location,
            Type = e.Type,
            Priority = e.Priority,
            Status = string.IsNullOrWhiteSpace(e.Status) ? "scheduled" : e.Status,
            IsActive = e.IsActive,
            RelatedToType = e.RelatedToType,
            RelatedToId = e.RelatedToId,
            CreatedBy = e.CreatedBy,
            CreatedAt = e.CreatedAt,
            ModifiedAt = e.ModifiedAt,
            ModifiedBy = e.ModifiedBy
        };

        public async Task<ApiResponse<List<SchedulerEventResponseDto>>> GetAll()
        {
            try
            {
                var list = await context.SchedulerEvents.AsNoTracking().OrderByDescending(e => e.StartTime).ToListAsync();
                return new ApiResponse<List<SchedulerEventResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<SchedulerEventResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<SchedulerEventResponseDto>> GetById(int id)
        {
            try
            {
                var e = await context.SchedulerEvents.FindAsync(id);
                if (e == null) return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = "Event not found" };
                return new ApiResponse<SchedulerEventResponseDto> { Success = true, Data = Map(e) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<SchedulerEventResponseDto>> Create(CreateSchedulerEventDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                var status = string.IsNullOrWhiteSpace(dto.Status) ? "scheduled" : dto.Status.Trim();
                var e = new SchedulerEvent
                {
                    Title = dto.Title.Trim(),
                    Description = dto.Description?.Trim() ?? string.Empty,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    Attendees = dto.Attendees ?? new List<int>(),
                    Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
                    Type = string.IsNullOrWhiteSpace(dto.Type) ? "meeting" : dto.Type.Trim(),
                    Priority = string.IsNullOrWhiteSpace(dto.Priority) ? "medium" : dto.Priority.Trim(),
                    Status = status,
                    IsActive = IsActiveForStatus(status),
                    RelatedToType = string.IsNullOrWhiteSpace(dto.RelatedToType) ? null : dto.RelatedToType.Trim(),
                    RelatedToId = dto.RelatedToId,
                    CreatedBy = AuditUserIds.System,
                    CreatedAt = now,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                context.SchedulerEvents.Add(e);
                await context.SaveChangesAsync();
                return new ApiResponse<SchedulerEventResponseDto> { Success = true, Data = Map(e) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<SchedulerEventResponseDto>> Update(int id, UpdateSchedulerEventDto dto)
        {
            try
            {
                var e = await context.SchedulerEvents.FindAsync(id);
                if (e == null) return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = "Event not found" };
                var status = string.IsNullOrWhiteSpace(dto.Status) ? "scheduled" : dto.Status.Trim();
                e.Title = dto.Title.Trim();
                e.Description = dto.Description?.Trim() ?? string.Empty;
                e.StartTime = dto.StartTime;
                e.EndTime = dto.EndTime;
                e.Attendees = dto.Attendees ?? new List<int>();
                e.Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim();
                e.Type = string.IsNullOrWhiteSpace(dto.Type) ? "meeting" : dto.Type.Trim();
                e.Priority = string.IsNullOrWhiteSpace(dto.Priority) ? "medium" : dto.Priority.Trim();
                e.Status = status;
                e.IsActive = IsActiveForStatus(status);
                e.RelatedToType = string.IsNullOrWhiteSpace(dto.RelatedToType) ? null : dto.RelatedToType.Trim();
                e.RelatedToId = dto.RelatedToId;
                e.ModifiedAt = DateTime.UtcNow;
                e.ModifiedBy = AuditUserIds.System;
                await context.SaveChangesAsync();
                return new ApiResponse<SchedulerEventResponseDto> { Success = true, Data = Map(e) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> Delete(int id)
        {
            try
            {
                var e = await context.SchedulerEvents.FindAsync(id);
                if (e == null) return new ApiResponse<bool> { Success = false, Message = "Event not found" };
                context.SchedulerEvents.Remove(e);
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
