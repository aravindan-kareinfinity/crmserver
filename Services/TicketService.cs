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
        Task<ApiResponse<List<TicketResponseDto>>> GetByCustomerCode(string customerCode);
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

        CrmDbContext context;

        public TicketService(CrmDbContext context)
        {
            this.context = context;
        }

        private readonly record struct TicketSnapshot(
            TicketStatus Status,
            TicketPriority Priority,
            int AssignedTo,
            string CustomerCode,
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
            t.CustomerCode,
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
            sb.Append($"Customer code {t.CustomerCode}, location #{t.LocationId}. ");
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
            if (!string.Equals(b.CustomerCode, a.CustomerCode, StringComparison.Ordinal))
                parts.Add($"Customer: {b.CustomerCode} → {a.CustomerCode}");
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
            context.TicketTimelines.Add(new TicketTimeline
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

        private static async Task EnrichTicketsAsync(CrmDbContext ctx, List<TicketResponseDto> rows) =>
            await EntityCodeResolution.EnrichTicketDtosAsync(ctx, rows);

        private static TicketResponseDto Map(Ticket t) => new()
        {
            Id = t.Id,
            CustomerId = t.Customer?.Id ?? 0,
            LocationId = t.LocationId,
            Subject = t.Subject,
            Description = t.Description,
            ContactPerson = t.ContactPerson,
            ContactMobile = t.ContactMobile,
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
                var total = await context.Tickets.CountAsync();
                var items = await context.Tickets.Include(t => t.Customer).OrderByDescending(t => t.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                var dtos = items.Select(Map).ToList();
                await EnrichTicketsAsync(context, dtos);
                return new ApiResponse<PaginatedResponse<TicketResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<TicketResponseDto>
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
                return new ApiResponse<PaginatedResponse<TicketResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TicketResponseDto>>> GetAll()
        {
            try
            {
                var list = await context.Tickets.Include(t => t.Customer).OrderByDescending(t => t.CreatedAt).ToListAsync();
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(context, dtos);
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = dtos };
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
                var t = await context.Tickets.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
                if (t == null) return new ApiResponse<TicketResponseDto> { Success = false, Message = "Ticket not found" };
                var one = Map(t);
                await EnrichTicketsAsync(context, new List<TicketResponseDto> { one });
                return new ApiResponse<TicketResponseDto> { Success = true, Data = one };
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
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = new List<TicketResponseDto>() };
                var list = await context.Tickets.Include(t => t.Customer).Where(t => t.CustomerCode == cc).OrderByDescending(t => t.CreatedAt).ToListAsync();
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(context, dtos);
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TicketResponseDto>>> GetByCustomerCode(string customerCode)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = err };
                return await GetByCustomerId(cid);
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
                var list = await context.Tickets.Include(t => t.Customer).Where(t => t.Status == st).OrderByDescending(t => t.CreatedAt).ToListAsync();
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(context, dtos);
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = dtos };
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
                var list = await context.Tickets.Include(t => t.Customer).Where(t => t.AssignedTo == userId).OrderByDescending(t => t.CreatedAt).ToListAsync();
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(context, dtos);
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = dtos };
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
                var (custCode, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, dto.CustomerId, dto.CustomerCode);
                if (cErr != null)
                    return new ApiResponse<TicketResponseDto> { Success = false, Message = cErr };
                var (locationId, lErr) = await EntityCodeResolution.ResolveRequiredLocationIdAsync(
                    context, custCode, dto.LocationId, dto.LocationCode);
                if (lErr != null)
                    return new ApiResponse<TicketResponseDto> { Success = false, Message = lErr };

                var now = DateTime.UtcNow;
                var ticket = new Ticket
                {
                    CustomerCode = custCode,
                    LocationId = locationId,
                    Subject = dto.Subject,
                    Description = dto.Description,
                    ContactPerson = string.IsNullOrWhiteSpace(dto.ContactPerson) ? null : dto.ContactPerson.Trim(),
                    ContactMobile = string.IsNullOrWhiteSpace(dto.ContactMobile) ? null : dto.ContactMobile.Trim(),
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
                context.Tickets.Add(ticket);
                await context.SaveChangesAsync();
                await context.Entry(ticket).Reference(t => t.Customer).LoadAsync();
                var actorCreate = ResolveTimelineActor(dto.ChangedByUserId, ticket.AssignedTo);
                AddTicketTimelineRow(ticket.Id, actorCreate, TimelineTypeSystem, BuildCreateTimelineNotes(ticket));
                await context.SaveChangesAsync();
                var created = Map(ticket);
                await EnrichTicketsAsync(context, new List<TicketResponseDto> { created });
                return new ApiResponse<TicketResponseDto> { Success = true, Data = created };
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
                var ticket = await context.Tickets.Include(t => t.Customer).FirstOrDefaultAsync(t => t.Id == id);
                if (ticket == null) return new ApiResponse<TicketResponseDto> { Success = false, Message = "Ticket not found" };
                var before = Snap(ticket);
                if (!string.IsNullOrWhiteSpace(dto.Status))
                    ticket.Status = Enum.Parse<TicketStatus>(dto.Status, true);
                if (!string.IsNullOrWhiteSpace(dto.Priority))
                    ticket.Priority = Enum.Parse<TicketPriority>(dto.Priority, true);
                if (dto.AssignedTo.HasValue) ticket.AssignedTo = dto.AssignedTo.Value;

                if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                {
                    var (cc, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, 0, dto.CustomerCode);
                    if (cErr != null)
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = cErr };
                    ticket.CustomerCode = cc;
                }
                else if (dto.CustomerId.HasValue)
                {
                    var (cc, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, dto.CustomerId.Value, null);
                    if (cErr != null)
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = cErr };
                    ticket.CustomerCode = cc;
                }

                if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                {
                    var (lid, lErr) = await EntityCodeResolution.ResolveRequiredLocationIdAsync(
                        context, ticket.CustomerCode, 0, dto.LocationCode);
                    if (lErr != null)
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = lErr };
                    ticket.LocationId = lid;
                }
                else if (dto.LocationId.HasValue)
                    ticket.LocationId = dto.LocationId.Value;
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

                await context.SaveChangesAsync();
                await context.Entry(ticket).Reference(t => t.Customer).LoadAsync();
                var updated = Map(ticket);
                await EnrichTicketsAsync(context, new List<TicketResponseDto> { updated });
                return new ApiResponse<TicketResponseDto> { Success = true, Data = updated };
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
                var t = await context.Tickets.FindAsync(id);
                if (t == null) return new ApiResponse<bool> { Success = false, Message = "Ticket not found" };
                context.Tickets.Remove(t);
                await context.SaveChangesAsync();
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
                var rows = await context.TicketTimelines.Where(x => x.TicketId == ticketId)
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
                var tk = await context.Tickets.FindAsync(ticketId);
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
                context.TicketTimelines.Add(e);
                await context.SaveChangesAsync();
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
