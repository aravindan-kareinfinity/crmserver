using System.Text;
using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>TicketService.ts</c>.</summary>
    public interface ITicketService
    {
        Task<ApiResponse<PaginatedResponse<TicketResponseDto>>> GetAllTickets(int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<TicketResponseDto>>> GetAll();
        Task<ApiResponse<TicketResponseDto>> GetTicketById(int id);
        Task<ApiResponse<List<TicketResponseDto>>> GetByCustomerId(int customerId);
        Task<ApiResponse<List<TicketResponseDto>>> GetByStatus(string status);
        Task<ApiResponse<List<TicketResponseDto>>> GetByAssignedTo(int userId);
        Task<ApiResponse<TicketResponseDto>> CreateTicket(CreateTicketDto dto);
        Task<ApiResponse<TicketResponseDto>> UpdateTicket(int id, UpdateTicketDto dto);
        Task<ApiResponse<bool>> DeleteTicket(int id);
        Task<ApiResponse<List<TicketTimelineEntryDto>>> GetTimeline(int ticketId);
        Task<ApiResponse<TicketTimelineEntryDto>> AddTimelineEntry(int ticketId, AddTicketTimelineEntryDto dto);
    }

    public class TicketService : ITicketService
    {
        private const int TimelineTypeSystem = 1;
        private const int TimelineTypeFieldUpdate = 2;

        private readonly CrmDbContext _context;

        public TicketService(CrmDbContext context)
        {
            _context = context;
        }

        private readonly record struct TicketSnapshot(
            TicketStatus Status,
            TicketPriority Priority,
            int AssignedTo,
            int CustomerId,
            int LocationId,
            string Subject,
            string Description,
            string Category,
            string? Module,
            bool IsActive);

        private static TicketSnapshot Snap(Ticket t) => new(
            t.Status,
            t.Priority,
            t.AssignedTo,
            t.CustomerId,
            t.LocationId,
            t.Subject,
            t.Description,
            t.Category,
            t.Module,
            t.IsActive);

        private static int ResolveTimelineActor(int? changedByUserId, int assigneeFallback)
        {
            if (changedByUserId is > 0) return changedByUserId.Value;
            if (assigneeFallback > 0) return assigneeFallback;
            return 1;
        }

        private static string Trunc(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            s = s.Trim();
            return s.Length <= max ? s : s[..max] + "…";
        }

        private static string ModLabel(string? m) => string.IsNullOrWhiteSpace(m) ? "(none)" : m.Trim();

        private static bool IsTicketTerminalStatus(TicketStatus s) =>
            s is TicketStatus.resolved or TicketStatus.closed;

        private static long ClosureActorUserId(int? changedByUserId) =>
            changedByUserId is > 0 ? changedByUserId.Value : AuditUserIds.System;

        private static string BuildCreateTimelineNotes(Ticket t)
        {
            var sb = new StringBuilder();
            sb.Append("Ticket created. ");
            sb.Append($"Subject: {Trunc(t.Subject, 120)}. ");
            sb.Append($"Customer #{t.CustomerId}, location #{t.LocationId}. ");
            sb.Append($"Assigned to user #{t.AssignedTo}. ");
            sb.Append($"Priority: {t.Priority}, status: {t.Status}. ");
            sb.Append($"Category: {t.Category}. ");
            sb.Append($"Module: {ModLabel(t.Module)}. ");
            if (!string.IsNullOrWhiteSpace(t.Description))
                sb.Append("Description was provided.");
            return sb.ToString().Trim();
        }

        private static string? BuildUpdateTimelineNotes(TicketSnapshot b, TicketSnapshot a)
        {
            var parts = new List<string>();
            if (b.Status != a.Status)
                parts.Add($"Status: {b.Status} → {a.Status}");
            if (b.Priority != a.Priority)
                parts.Add($"Priority: {b.Priority} → {a.Priority}");
            if (b.AssignedTo != a.AssignedTo)
                parts.Add($"Assigned to: user #{b.AssignedTo} → user #{a.AssignedTo}");
            if (b.CustomerId != a.CustomerId)
                parts.Add($"Customer: #{b.CustomerId} → #{a.CustomerId}");
            if (b.LocationId != a.LocationId)
                parts.Add($"Location: #{b.LocationId} → #{a.LocationId}");
            if (!string.Equals(b.Subject, a.Subject, StringComparison.Ordinal))
                parts.Add($"Subject: {Trunc(b.Subject, 80)} → {Trunc(a.Subject, 80)}");
            if (!string.Equals(b.Description, a.Description, StringComparison.Ordinal))
                parts.Add("Description updated");
            if (!string.Equals(b.Category, a.Category, StringComparison.Ordinal))
                parts.Add($"Category: {Trunc(b.Category, 60)} → {Trunc(a.Category, 60)}");
            var bm = ModLabel(b.Module);
            var am = ModLabel(a.Module);
            if (!string.Equals(bm, am, StringComparison.Ordinal))
                parts.Add($"Module: {bm} → {am}");
            if (b.IsActive != a.IsActive)
                parts.Add($"Active: {b.IsActive} → {a.IsActive}");
            return parts.Count == 0
                ? null
                : "Ticket updated:\n• " + string.Join("\n• ", parts);
        }

        private void AddTicketTimelineRow(int ticketId, int userId, int type, string notes)
        {
            var now = DateTime.UtcNow;
            _context.TicketTimelines.Add(new TicketTimeline
            {
                TicketId = ticketId,
                UserId = userId,
                Type = type,
                Notes = notes,
                FileId = null,
                FileName = null,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId > 0 ? userId : AuditUserIds.System,
                ModifiedAt = now,
                ModifiedBy = AuditUserIds.System
            });
        }

        private static TicketResponseDto Map(Ticket t) => new()
        {
            Id = t.Id,
            CustomerId = t.CustomerId,
            LocationId = t.LocationId,
            Subject = t.Subject,
            Description = t.Description,
            Status = t.Status.ToString(),
            Priority = t.Priority.ToString(),
            AssignedTo = t.AssignedTo,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            ModifiedAt = t.ModifiedAt,
            ModifiedBy = t.ModifiedBy,
            ClosedAt = t.ClosedAt,
            ClosedBy = t.ClosedBy,
            Category = t.Category,
            Module = t.Module
        };

        public async Task<ApiResponse<PaginatedResponse<TicketResponseDto>>> GetAllTickets(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var total = await _context.Tickets.CountAsync();
                var items = await _context.Tickets.OrderByDescending(t => t.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                return new ApiResponse<PaginatedResponse<TicketResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<TicketResponseDto>
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
                return new ApiResponse<PaginatedResponse<TicketResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TicketResponseDto>>> GetAll()
        {
            try
            {
                var list = await _context.Tickets.OrderByDescending(t => t.CreatedAt).ToListAsync();
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TicketResponseDto>> GetTicketById(int id)
        {
            try
            {
                var t = await _context.Tickets.FindAsync(id);
                if (t == null) return new ApiResponse<TicketResponseDto> { Success = false, Message = "Ticket not found" };
                return new ApiResponse<TicketResponseDto> { Success = true, Data = Map(t) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TicketResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TicketResponseDto>>> GetByCustomerId(int customerId)
        {
            try
            {
                var list = await _context.Tickets.Where(t => t.CustomerId == customerId).OrderByDescending(t => t.CreatedAt).ToListAsync();
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TicketResponseDto>>> GetByStatus(string status)
        {
            try
            {
                var st = Enum.Parse<TicketStatus>(status, true);
                var list = await _context.Tickets.Where(t => t.Status == st).OrderByDescending(t => t.CreatedAt).ToListAsync();
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch
            {
                return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = $"Invalid ticket status: {status}" };
            }
        }

        public async Task<ApiResponse<List<TicketResponseDto>>> GetByAssignedTo(int userId)
        {
            try
            {
                var list = await _context.Tickets.Where(t => t.AssignedTo == userId).OrderByDescending(t => t.CreatedAt).ToListAsync();
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = list.Select(Map).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TicketResponseDto>> CreateTicket(CreateTicketDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                var ticket = new Ticket
                {
                    CustomerId = dto.CustomerId,
                    LocationId = dto.LocationId,
                    Subject = dto.Subject,
                    Description = dto.Description,
                    Status = TicketStatus.open,
                    Priority = Enum.Parse<TicketPriority>(dto.Priority, true),
                    AssignedTo = dto.AssignedTo,
                    Category = dto.Category,
                    Module = string.IsNullOrWhiteSpace(dto.Module) ? null : dto.Module.Trim(),
                    SlaDeadline = DateTime.UtcNow,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = ClosureActorUserId(dto.ChangedByUserId),
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();
                var actorCreate = ResolveTimelineActor(dto.ChangedByUserId, ticket.AssignedTo);
                AddTicketTimelineRow(ticket.Id, actorCreate, TimelineTypeSystem, BuildCreateTimelineNotes(ticket));
                await _context.SaveChangesAsync();
                return new ApiResponse<TicketResponseDto> { Success = true, Data = Map(ticket) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TicketResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TicketResponseDto>> UpdateTicket(int id, UpdateTicketDto dto)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null) return new ApiResponse<TicketResponseDto> { Success = false, Message = "Ticket not found" };
                var before = Snap(ticket);
                if (!string.IsNullOrWhiteSpace(dto.Status))
                    ticket.Status = Enum.Parse<TicketStatus>(dto.Status, true);
                if (!string.IsNullOrWhiteSpace(dto.Priority))
                    ticket.Priority = Enum.Parse<TicketPriority>(dto.Priority, true);
                if (dto.AssignedTo.HasValue) ticket.AssignedTo = dto.AssignedTo.Value;
                if (dto.CustomerId.HasValue) ticket.CustomerId = dto.CustomerId.Value;
                if (dto.LocationId.HasValue) ticket.LocationId = dto.LocationId.Value;
                if (dto.Subject != null) ticket.Subject = dto.Subject.Trim();
                if (dto.Description != null) ticket.Description = dto.Description;
                if (dto.Category != null)
                    ticket.Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim();
                if (dto.Module != null)
                    ticket.Module = string.IsNullOrWhiteSpace(dto.Module) ? null : dto.Module.Trim();
                if (dto.IsActive.HasValue) ticket.IsActive = dto.IsActive.Value;

                // Closure stamps: explicit `closed` always refreshes closed_at/closed_by (e.g. after resolved).
                // First move to `resolved` from active also stamps; reopening clears.
                if (ticket.Status == TicketStatus.closed && before.Status != TicketStatus.closed)
                {
                    ticket.ClosedAt = DateTime.UtcNow;
                    ticket.ClosedBy = ClosureActorUserId(dto.ChangedByUserId);
                }
                else if (ticket.Status == TicketStatus.resolved && !IsTicketTerminalStatus(before.Status))
                {
                    ticket.ClosedAt = DateTime.UtcNow;
                    ticket.ClosedBy = ClosureActorUserId(dto.ChangedByUserId);
                }
                else if (!IsTicketTerminalStatus(ticket.Status) && IsTicketTerminalStatus(before.Status))
                {
                    ticket.ClosedAt = null;
                    ticket.ClosedBy = null;
                }

                ticket.ModifiedAt = DateTime.UtcNow;
                ticket.ModifiedBy = AuditUserIds.System;
                var after = Snap(ticket);
                var updateNotes = BuildUpdateTimelineNotes(before, after);
                if (updateNotes != null)
                {
                    var actorUpdate = ResolveTimelineActor(dto.ChangedByUserId, ticket.AssignedTo);
                    AddTicketTimelineRow(ticket.Id, actorUpdate, TimelineTypeFieldUpdate, updateNotes);
                }

                await _context.SaveChangesAsync();
                return new ApiResponse<TicketResponseDto> { Success = true, Data = Map(ticket) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<TicketResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteTicket(int id)
        {
            try
            {
                var t = await _context.Tickets.FindAsync(id);
                if (t == null) return new ApiResponse<bool> { Success = false, Message = "Ticket not found" };
                _context.Tickets.Remove(t);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TicketTimelineEntryDto>>> GetTimeline(int ticketId)
        {
            try
            {
                var rows = await _context.TicketTimelines.Where(x => x.TicketId == ticketId)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new TicketTimelineEntryDto
                    {
                        Id = x.Id,
                        TicketId = x.TicketId,
                        UserId = x.UserId,
                        Type = x.Type,
                        Notes = x.Notes,
                        FileId = x.FileId,
                        FileName = x.FileName,
                        IsActive = x.IsActive,
                        CreatedAt = x.CreatedAt,
                        CreatedBy = x.CreatedBy,
                        ModifiedAt = x.ModifiedAt,
                        ModifiedBy = x.ModifiedBy
                    }).ToListAsync();
                return new ApiResponse<List<TicketTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TicketTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TicketTimelineEntryDto>> AddTimelineEntry(int ticketId, AddTicketTimelineEntryDto dto)
        {
            try
            {
                var tk = await _context.Tickets.FindAsync(ticketId);
                if (tk == null) return new ApiResponse<TicketTimelineEntryDto> { Success = false, Message = "Ticket not found" };
                var now = DateTime.UtcNow;
                var e = new TicketTimeline
                {
                    TicketId = ticketId,
                    UserId = dto.UserId,
                    Type = dto.Type,
                    Notes = dto.Notes,
                    FileId = dto.FileId,
                    FileName = dto.FileName,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = dto.UserId > 0 ? dto.UserId : AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.TicketTimelines.Add(e);
                await _context.SaveChangesAsync();
                return new ApiResponse<TicketTimelineEntryDto>
                {
                    Success = true,
                    Data = new TicketTimelineEntryDto
                    {
                        Id = e.Id,
                        TicketId = e.TicketId,
                        UserId = e.UserId,
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
                return new ApiResponse<TicketTimelineEntryDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
