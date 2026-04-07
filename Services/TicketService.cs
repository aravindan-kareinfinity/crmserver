using System.Text;
using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;
using System.Globalization;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>TicketService.ts</c>.</summary>
    public interface ITicketService
    {
        Task<ApiResponse<PaginatedResponse<TicketResponseDto>>> GetAllTickets(int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<TicketResponseDto>>> GetAll(TicketQueryDto q);
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

        IDbProvider dbprovider;

        public TicketService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        private readonly record struct TicketSnapshot(
            int StatusId,
            TicketPriority Priority,
            int AssignedTo,
            string CustomerCode,
            int LocationId,
            string Subject,
            string Description,
            int CategoryId,
            int? ModuleId,
            bool IsActive);

        private static TicketSnapshot Snap(Ticket t) => new(
            t.StatusId,
            t.Priority,
            t.AssignedTo,
            t.CustomerCode,
            t.LocationId,
            t.Subject,
            t.Description,
            t.CategoryId,
            t.ModuleId,
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

        private static long ClosureActorUserId(int? changedByUserId) =>
            changedByUserId is > 0 ? changedByUserId.Value : AuditUserIds.System;

        private static string BuildCreateTimelineNotes(Ticket t)
        {
            var sb = new StringBuilder();
            sb.Append("Ticket created. ");
            sb.Append($"Subject: {Trunc(t.Subject, 120)}. ");
            sb.Append($"Customer code {t.CustomerCode}, location #{t.LocationId}. ");
            sb.Append($"Assigned to user #{t.AssignedTo}. ");
            sb.Append($"Priority: {t.Priority}, statusId: {t.StatusId}. ");
            sb.Append($"CategoryId: {t.CategoryId}. ");
            sb.Append($"ModuleId: {(t.ModuleId.HasValue ? t.ModuleId.Value : 0)}. ");
            if (!string.IsNullOrWhiteSpace(t.Description))
                sb.Append("Description was provided.");
            return sb.ToString().Trim();
        }

        private static string? BuildUpdateTimelineNotes(TicketSnapshot b, TicketSnapshot a)
        {
            var parts = new List<string>();
            if (b.StatusId != a.StatusId)
                parts.Add($"StatusId: {b.StatusId} → {a.StatusId}");
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
            if (b.CategoryId != a.CategoryId)
                parts.Add($"CategoryId: {b.CategoryId} → {a.CategoryId}");
            if ((b.ModuleId ?? 0) != (a.ModuleId ?? 0))
                parts.Add($"ModuleId: {(b.ModuleId ?? 0)} → {(a.ModuleId ?? 0)}");
            if (b.IsActive != a.IsActive)
                parts.Add($"Active: {b.IsActive} → {a.IsActive}");
            return parts.Count == 0
                ? null
                : "Ticket updated:\n• " + string.Join("\n• ", parts);
        }

        private static async Task AddTicketTimelineRowAsync(IDb db, int ticketId, int userId, int type, string notes)
        {
            var now = DateTime.UtcNow;
            var cmd = db.GetCommand(@"
INSERT INTO ticket_timelines (
    ticket_id, user_id, type, notes, file_id, file_name,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @ticket_id, @user_id, @type, @notes, NULL, NULL,
    true, @created_at, @created_by, @modified_at, @modified_by
);");
            db.AddParameter(cmd, "ticket_id", DbTypes.Types.Integer).Value = ticketId;
            db.AddParameter(cmd, "user_id", DbTypes.Types.Integer).Value = userId;
            db.AddParameter(cmd, "type", DbTypes.Types.Integer).Value = type;
            db.AddParameter(cmd, "notes", DbTypes.Types.String).Value = notes;
            db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
            db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = userId > 0 ? userId : AuditUserIds.System;
            db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
            db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
            await db.ExecuteNonQuery(cmd);
        }

        private async Task EnrichTicketsAsync(List<TicketResponseDto> rows)
        {
            if (rows.Count == 0) return;
            var custIds = rows.Select(r => r.CustomerId).Where(id => id > 0).Distinct().ToList();
            var locIds = rows.Select(r => r.LocationId).Where(id => id > 0).Distinct().ToList();
            if (custIds.Count == 0 && locIds.Count == 0) return;

            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                Dictionary<int, string?> custMap = new();
                if (custIds.Count > 0)
                {
                    var p = custIds.Select((_, i) => $"@c{i}").ToList();
                    var cmd = db.GetCommand($@"
SELECT id, code
FROM customers
WHERE id IN ({string.Join(", ", p)});");
                    for (int i = 0; i < custIds.Count; i++)
                        db.AddParameter(cmd, $"c{i}", DbTypes.Types.Integer).Value = custIds[i];
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            custMap[r.GetInt32(r.GetOrdinal("id"))] = r.IsDBNull(r.GetOrdinal("code")) ? null : r.GetString(r.GetOrdinal("code"));
                    }
                }

                Dictionary<int, string> locMap = new();
                if (locIds.Count > 0)
                {
                    var p = locIds.Select((_, i) => $"@l{i}").ToList();
                    var cmd = db.GetCommand($@"
SELECT id, code
FROM locations
WHERE id IN ({string.Join(", ", p)});");
                    for (int i = 0; i < locIds.Count; i++)
                        db.AddParameter(cmd, $"l{i}", DbTypes.Types.Integer).Value = locIds[i];
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            locMap[r.GetInt32(r.GetOrdinal("id"))] = r.GetString(r.GetOrdinal("code"));
                    }
                }

                foreach (var row in rows)
                {
                    row.CustomerCode = custMap.GetValueOrDefault(row.CustomerId);
                    if (locMap.TryGetValue(row.LocationId, out var lc))
                        row.LocationCode = lc;
                }
            }
        }

        private static Ticket ReadTicket(DbDataReader r)
        {
            return new Ticket
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                CustomerId = r.GetInt32(r.GetOrdinal("customer_id")),
                CustomerCode = r.GetString(r.GetOrdinal("customer_code")),
                LocationId = r.GetInt32(r.GetOrdinal("location_id")),
                Subject = r.GetString(r.GetOrdinal("subject")),
                Description = r.GetString(r.GetOrdinal("description")),
                ContactPerson = r.IsDBNull(r.GetOrdinal("contact_person")) ? null : r.GetString(r.GetOrdinal("contact_person")),
                ContactMobile = r.IsDBNull(r.GetOrdinal("contact_mobile")) ? null : r.GetString(r.GetOrdinal("contact_mobile")),
                StatusId = r.GetInt32(r.GetOrdinal("status_id")),
                Priority = Enum.Parse<TicketPriority>(r.GetString(r.GetOrdinal("priority")), true),
                AssignedTo = r.GetInt32(r.GetOrdinal("assigned_to")),
                SlaDeadline = r.GetDateTime(r.GetOrdinal("sla_deadline")),
                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                CreatedBy = r.IsDBNull(r.GetOrdinal("created_by")) ? null : r.GetInt64(r.GetOrdinal("created_by")),
                ClosedAt = r.IsDBNull(r.GetOrdinal("closed_at")) ? null : r.GetDateTime(r.GetOrdinal("closed_at")),
                ClosedBy = r.IsDBNull(r.GetOrdinal("closed_by")) ? null : r.GetInt64(r.GetOrdinal("closed_by")),
                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
                CategoryId = r.GetInt32(r.GetOrdinal("category_id")),
                ModuleId = r.IsDBNull(r.GetOrdinal("module_id")) ? null : r.GetInt32(r.GetOrdinal("module_id")),
            };
        }

        private async Task<string?> GetCustomerCodeByIdAsync(IDb db, int customerId)
        {
            if (customerId <= 0) return null;
            var cmd = db.GetCommand("SELECT code FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = customerId;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync())
                    return r.IsDBNull(r.GetOrdinal("code")) ? null : r.GetString(r.GetOrdinal("code"));
            }
            return null;
        }

        private async Task<int?> GetCustomerIdByCodeAsync(IDb db, string customerCode)
        {
            var cmd = db.GetCommand("SELECT id FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "code", DbTypes.Types.String).Value = customerCode.Trim();
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync())
                    return r.GetInt32(r.GetOrdinal("id"));
            }
            return null;
        }

        private async Task<(string CustomerCode, int CustomerId, string? Error)> ResolveCustomerLinkAsync(IDb db, int customerId, string? customerCode)
        {
            if (!string.IsNullOrWhiteSpace(customerCode))
            {
                var trimmed = customerCode.Trim();
                var cmd = db.GetCommand("SELECT id, code FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
                db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                using (DbDataReader r = await db.Execute(cmd))
                {
                    if (!await r.ReadAsync())
                        return (string.Empty, 0, $"Unknown customer code: \"{trimmed}\"");
                    return (r.GetString(r.GetOrdinal("code")), r.GetInt32(r.GetOrdinal("id")), null);
                }
            }

            if (customerId <= 0)
                return (string.Empty, 0, "Provide customerId or customerCode");

            var byId = db.GetCommand("SELECT id, code FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
            db.AddParameter(byId, "id", DbTypes.Types.Integer).Value = customerId;
            using (DbDataReader r2 = await db.Execute(byId))
            {
                if (!await r2.ReadAsync())
                    return (string.Empty, 0, "Customer not found or has no code assigned");
                var code = r2.IsDBNull(r2.GetOrdinal("code")) ? "" : r2.GetString(r2.GetOrdinal("code"));
                if (string.IsNullOrWhiteSpace(code))
                    return (string.Empty, 0, "Customer not found or has no code assigned");
                return (code, r2.GetInt32(r2.GetOrdinal("id")), null);
            }
        }

        private async Task<(int LocationId, string? Error)> ResolveRequiredLocationIdAsync(
            IDb db,
            string customerCode,
            int locationId,
            string? locationCode)
        {
            if (string.IsNullOrWhiteSpace(customerCode))
                return (0, "Customer code is required to resolve location");

            var cc = customerCode.Trim();
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                var trimmed = locationCode.Trim();
                var cmd = db.GetCommand(@"
SELECT id
FROM locations
WHERE customer_code=@cc AND code=@code
LIMIT 1;");
                db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                using (DbDataReader r = await db.Execute(cmd))
                {
                    if (!await r.ReadAsync())
                        return (0, $"Unknown location code \"{trimmed}\" for this customer");
                    return (r.GetInt32(r.GetOrdinal("id")), null);
                }
            }

            if (locationId <= 0)
                return (0, "Provide locationId or locationCode");
            return (locationId, null);
        }

        private static async Task<int?> ResolveTicketStatusIdAsync(IDb db, int? incomingStatusId)
        {
            if (incomingStatusId is null or <= 0) return null;
            var cmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE id=@id
  AND is_active=true
  AND category='Ticket Status'
LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = incomingStatusId.Value;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (!await r.ReadAsync()) return null;
                return r.GetInt32(r.GetOrdinal("id"));
            }
        }

        private static async Task<int?> ResolveTicketStatusIdByValueAsync(IDb db, string value)
        {
            var v = (value ?? "").Trim();
            if (v.Length == 0) return null;
            var cmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE is_active=true
  AND category='Ticket Status'
  AND lower(value)=lower(@v)
ORDER BY sort_order ASC
LIMIT 1;");
            db.AddParameter(cmd, "v", DbTypes.Types.String).Value = v;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (!await r.ReadAsync()) return null;
                return r.GetInt32(r.GetOrdinal("id"));
            }
        }

        private static async Task<string?> ResolveReferenceLabelByIdAsync(IDb db, int id, string category)
        {
            if (id <= 0) return null;
            var cmd = db.GetCommand(@"
SELECT label
FROM reference_entries
WHERE id=@id AND is_active=true AND category=@cat
LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
            db.AddParameter(cmd, "cat", DbTypes.Types.String).Value = category;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (!await r.ReadAsync()) return null;
                return r.IsDBNull(r.GetOrdinal("label")) ? null : r.GetString(r.GetOrdinal("label"));
            }
        }

        private static TicketResponseDto Map(Ticket t) => new()
        {
            Id = t.Id,
            CustomerId = t.Customer?.Id ?? t.CustomerId,
            LocationId = t.LocationId,
            Subject = t.Subject,
            Description = t.Description,
            ContactPerson = t.ContactPerson,
            ContactMobile = t.ContactMobile,
            StatusId = t.StatusId,
            Priority = t.Priority.ToString(),
            AssignedTo = t.AssignedTo,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            ModifiedAt = t.ModifiedAt,
            ModifiedBy = t.ModifiedBy,
            ClosedAt = t.ClosedAt,
            ClosedBy = t.ClosedBy,
            CategoryId = t.CategoryId,
            ModuleId = t.ModuleId
        };

        public async Task<ApiResponse<PaginatedResponse<TicketResponseDto>>> GetAllTickets(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var offset = Math.Max(0, (pageNumber - 1) * pageSize);
                int total = 0;
                var items = new List<Ticket>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var countCmd = db.GetCommand("SELECT COUNT(*)::int AS total FROM tickets WHERE is_active=true;");
                    using (DbDataReader r = await db.Execute(countCmd))
                    {
                        if (await r.ReadAsync())
                            total = r.GetInt32(r.GetOrdinal("total"));
                    }
                    var cmd = db.GetCommand(@"
SELECT *
FROM tickets
ORDER BY id DESC
LIMIT @limit OFFSET @offset;");
                    db.AddParameter(cmd, "limit", DbTypes.Types.Integer).Value = pageSize;
                    db.AddParameter(cmd, "offset", DbTypes.Types.Integer).Value = offset;
                    using (DbDataReader r2 = await db.Execute(cmd))
                    {
                        while (await r2.ReadAsync())
                            items.Add(ReadTicket(r2));
                    }
                    var codes = items.Select(x => x.CustomerCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                    if (codes.Count > 0)
                    {
                        var p = codes.Select((_, i) => $"@c{i}").ToList();
                        var cmd2 = db.GetCommand($@"SELECT code, id FROM customers WHERE code IN ({string.Join(", ", p)});");
                        for (int i = 0; i < codes.Count; i++)
                            db.AddParameter(cmd2, $"c{i}", DbTypes.Types.String).Value = codes[i];
                        var map = new Dictionary<string, int>(StringComparer.Ordinal);
                        using (DbDataReader r3 = await db.Execute(cmd2))
                        {
                            while (await r3.ReadAsync())
                                map[r3.GetString(r3.GetOrdinal("code"))] = r3.GetInt32(r3.GetOrdinal("id"));
                        }
                        foreach (var t in items)
                        {
                            if (map.TryGetValue(t.CustomerCode, out var cid))
                                t.Customer = new Customer { Id = cid };
                        }
                    }
                }

                var dtos = items.Select(Map).ToList();
                await EnrichTicketsAsync(dtos);
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

        public async Task<ApiResponse<List<TicketResponseDto>>> GetAll(TicketQueryDto q)
        {
            try
            {
                var list = new List<Ticket>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var where = new List<string>();
                    if (!(q.IncludeInactive ?? false)) where.Add("is_active=true");
                    if (q.StatusId is > 0) where.Add("status_id=@status_id");
                    if (!string.IsNullOrWhiteSpace(q.Priority)) where.Add("lower(priority::text)=lower(@priority)");
                    if (q.CustomerId is > 0) where.Add("customer_id=@customer_id");
                    if (q.AssignedTo is > 0) where.Add("assigned_to=@assigned_to");
                    if (q.CategoryId is > 0) where.Add("category_id=@category_id");
                    if (q.ModuleId is > 0) where.Add("module_id=@module_id");
                    if (q.From != null) where.Add("created_at >= @from");
                    if (q.To != null) where.Add("created_at < @to");
                    if (!string.IsNullOrWhiteSpace(q.Search))
                    {
                        where.Add("(subject ILIKE @search OR description ILIKE @search)");
                    }

                    var sql = "SELECT * FROM tickets";
                    if (where.Count > 0) sql += " WHERE " + string.Join(" AND ", where);
                    sql += " ORDER BY id DESC;";
                    var cmd = db.GetCommand(sql);
                    if (q.StatusId is > 0) db.AddParameter(cmd, "status_id", DbTypes.Types.Integer).Value = q.StatusId.Value;
                    if (!string.IsNullOrWhiteSpace(q.Priority)) db.AddParameter(cmd, "priority", DbTypes.Types.String).Value = q.Priority!.Trim();
                    if (q.CustomerId is > 0) db.AddParameter(cmd, "customer_id", DbTypes.Types.Integer).Value = q.CustomerId.Value;
                    if (q.AssignedTo is > 0) db.AddParameter(cmd, "assigned_to", DbTypes.Types.Integer).Value = q.AssignedTo.Value;
                    if (q.CategoryId is > 0) db.AddParameter(cmd, "category_id", DbTypes.Types.Integer).Value = q.CategoryId.Value;
                    if (q.ModuleId is > 0) db.AddParameter(cmd, "module_id", DbTypes.Types.Integer).Value = q.ModuleId.Value;
                    if (q.From != null) db.AddParameter(cmd, "from", DbTypes.Types.DateTime).Value = q.From.Value.Date;
                    if (q.To != null) db.AddParameter(cmd, "to", DbTypes.Types.DateTime).Value = q.To.Value.Date.AddDays(1);
                    if (!string.IsNullOrWhiteSpace(q.Search)) db.AddParameter(cmd, "search", DbTypes.Types.String).Value = "%" + q.Search!.Trim() + "%";
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadTicket(r));
                    }
                    var codes = list.Select(x => x.CustomerCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                    if (codes.Count > 0)
                    {
                        var p = codes.Select((_, i) => $"@c{i}").ToList();
                        var cmd2 = db.GetCommand($@"SELECT code, id FROM customers WHERE code IN ({string.Join(", ", p)});");
                        for (int i = 0; i < codes.Count; i++)
                            db.AddParameter(cmd2, $"c{i}", DbTypes.Types.String).Value = codes[i];
                        var map = new Dictionary<string, int>(StringComparer.Ordinal);
                        using (DbDataReader r2 = await db.Execute(cmd2))
                        {
                            while (await r2.ReadAsync())
                                map[r2.GetString(r2.GetOrdinal("code"))] = r2.GetInt32(r2.GetOrdinal("id"));
                        }
                        foreach (var t in list)
                        {
                            if (map.TryGetValue(t.CustomerCode, out var cid))
                                t.Customer = new Customer { Id = cid };
                        }
                    }
                }
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(dtos);
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
                Ticket? t = null;
                int customerId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT * FROM tickets WHERE id=@id AND is_active=true LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            t = ReadTicket(r);
                    }
                    if (t != null)
                        customerId = t.CustomerId > 0 ? t.CustomerId : await GetCustomerIdByCodeAsync(db, t.CustomerCode) ?? 0;
                }
                if (t == null) return new ApiResponse<TicketResponseDto> { Success = false, Message = "Ticket not found" };
                t.CustomerId = customerId;
                t.Customer = new Customer { Id = customerId };
                var one = Map(t);
                await EnrichTicketsAsync(new List<TicketResponseDto> { one });
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
                string? cc;
                using (IDb db0 = await dbprovider.GetDb())
                {
                    await db0.Connect();
                    cc = await GetCustomerCodeByIdAsync(db0, customerId);
                }
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = new List<TicketResponseDto>() };
                var list = new List<Ticket>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM tickets
WHERE customer_id=@cid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadTicket(r));
                    }
                }
                foreach (var t in list)
                {
                    t.CustomerId = customerId;
                    t.Customer = new Customer { Id = customerId };
                }
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(dtos);
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
                int cid = 0;
                string? err = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var res = await ResolveCustomerLinkAsync(db, 0, customerCode);
                    cid = res.CustomerId;
                    err = res.Error;
                }
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
                var list = new List<Ticket>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    if (!int.TryParse((status ?? "").Trim(), out var stId) || stId <= 0)
                        return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = $"Invalid ticket status id: {status}" };
                    var resolvedId = await ResolveTicketStatusIdAsync(db, stId);
                    if (resolvedId is null)
                        return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = $"Invalid ticket status id: {status}" };
                    var cmd = db.GetCommand(@"
SELECT *
FROM tickets
WHERE status_id=@st
ORDER BY id DESC;");
                    db.AddParameter(cmd, "st", DbTypes.Types.Integer).Value = resolvedId.Value;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadTicket(r));
                    }
                    var codes = list.Select(x => x.CustomerCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                    if (codes.Count > 0)
                    {
                        var p = codes.Select((_, i) => $"@c{i}").ToList();
                        var cmd2 = db.GetCommand($@"SELECT code, id FROM customers WHERE code IN ({string.Join(", ", p)});");
                        for (int i = 0; i < codes.Count; i++)
                            db.AddParameter(cmd2, $"c{i}", DbTypes.Types.String).Value = codes[i];
                        var map = new Dictionary<string, int>(StringComparer.Ordinal);
                        using (DbDataReader r2 = await db.Execute(cmd2))
                        {
                            while (await r2.ReadAsync())
                                map[r2.GetString(r2.GetOrdinal("code"))] = r2.GetInt32(r2.GetOrdinal("id"));
                        }
                        foreach (var t in list)
                        {
                            if (map.TryGetValue(t.CustomerCode, out var cid))
                                t.Customer = new Customer { Id = cid };
                        }
                    }
                }
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(dtos);
                return new ApiResponse<List<TicketResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TicketResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TicketResponseDto>>> GetByAssignedTo(int userId)
        {
            try
            {
                var list = new List<Ticket>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM tickets
WHERE assigned_to=@uid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "uid", DbTypes.Types.Integer).Value = userId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                            list.Add(ReadTicket(r));
                    }
                    var codes = list.Select(x => x.CustomerCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                    if (codes.Count > 0)
                    {
                        var p = codes.Select((_, i) => $"@c{i}").ToList();
                        var cmd2 = db.GetCommand($@"SELECT code, id FROM customers WHERE code IN ({string.Join(", ", p)});");
                        for (int i = 0; i < codes.Count; i++)
                            db.AddParameter(cmd2, $"c{i}", DbTypes.Types.String).Value = codes[i];
                        var map = new Dictionary<string, int>(StringComparer.Ordinal);
                        using (DbDataReader r2 = await db.Execute(cmd2))
                        {
                            while (await r2.ReadAsync())
                                map[r2.GetString(r2.GetOrdinal("code"))] = r2.GetInt32(r2.GetOrdinal("id"));
                        }
                        foreach (var t in list)
                        {
                            if (map.TryGetValue(t.CustomerCode, out var cid))
                                t.Customer = new Customer { Id = cid };
                        }
                    }
                }
                var dtos = list.Select(Map).ToList();
                await EnrichTicketsAsync(dtos);
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
                if (string.IsNullOrWhiteSpace(dto.ContactPerson))
                    return new ApiResponse<TicketResponseDto> { Success = false, Message = "Contact person is required" };
                if (string.IsNullOrWhiteSpace(dto.ContactMobile))
                    return new ApiResponse<TicketResponseDto> { Success = false, Message = "Contact mobile is required" };

                var now = DateTime.UtcNow;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        var (custCode, custId, cErr) = await ResolveCustomerLinkAsync(db, dto.CustomerId, dto.CustomerCode);
                        if (cErr != null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<TicketResponseDto> { Success = false, Message = cErr };
                        }
                        var (locationId, lErr) = await ResolveRequiredLocationIdAsync(db, custCode, dto.LocationId, dto.LocationCode);
                        if (lErr != null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<TicketResponseDto> { Success = false, Message = lErr };
                        }

                        var statusId =
                            await ResolveTicketStatusIdAsync(db, dto.StatusId)
                            ?? await ResolveTicketStatusIdByValueAsync(db, "open")
                            ?? 0;
                        if (statusId <= 0)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<TicketResponseDto> { Success = false, Message = "No active Ticket Status reference entry found" };
                        }
                        var priority = Enum.Parse<TicketPriority>(dto.Priority, true);
                        var createdBy = ClosureActorUserId(dto.ChangedByUserId);
                        var categoryLabel = await ResolveReferenceLabelByIdAsync(db, dto.CategoryId, "Ticket Category") ?? "General";
                        var moduleLabel = dto.ModuleId.HasValue && dto.ModuleId.Value > 0
                            ? await ResolveReferenceLabelByIdAsync(db, dto.ModuleId.Value, "Ticket Module")
                            : null;
                        var cmd = db.GetCommand(@"
INSERT INTO tickets (
    customer_id, customer_code, location_id, subject, description,
    contact_person, contact_mobile, status_id, priority,
    assigned_to, sla_deadline, is_active, created_at, created_by,
    closed_at, closed_by, modified_at, modified_by,
    category, module,
    category_id, module_id
)
VALUES (
    @customer_id, @customer_code, @location_id, @subject, @description,
    @contact_person, @contact_mobile, @status_id, @priority::ticket_priority,
    @assigned_to, @sla_deadline, true, @created_at, @created_by,
    NULL, NULL, @modified_at, @modified_by,
    @category, @module,
    @category_id, @module_id
)
RETURNING id;");
                        db.AddParameter(cmd, "customer_id", DbTypes.Types.Integer).Value = custId;
                        db.AddParameter(cmd, "customer_code", DbTypes.Types.String).Value = custCode;
                        db.AddParameter(cmd, "location_id", DbTypes.Types.Integer).Value = locationId;
                        db.AddParameter(cmd, "subject", DbTypes.Types.String).Value = dto.Subject;
                        db.AddParameter(cmd, "description", DbTypes.Types.String).Value = dto.Description;
                        db.AddParameter(cmd, "contact_person", DbTypes.Types.String).Value =
                            string.IsNullOrWhiteSpace(dto.ContactPerson) ? (object)DBNull.Value : dto.ContactPerson.Trim();
                        db.AddParameter(cmd, "contact_mobile", DbTypes.Types.String).Value =
                            string.IsNullOrWhiteSpace(dto.ContactMobile) ? (object)DBNull.Value : dto.ContactMobile.Trim();
                        db.AddParameter(cmd, "status_id", DbTypes.Types.Integer).Value = statusId;
                        db.AddParameter(cmd, "priority", DbTypes.Types.String).Value = priority.ToString();
                        db.AddParameter(cmd, "assigned_to", DbTypes.Types.Integer).Value = dto.AssignedTo;
                        db.AddParameter(cmd, "sla_deadline", DbTypes.Types.DateTime).Value = DateTime.UtcNow;
                        db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = createdBy;
                        db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                        db.AddParameter(cmd, "category", DbTypes.Types.String).Value = categoryLabel;
                        db.AddParameter(cmd, "module", DbTypes.Types.String).Value = moduleLabel != null ? moduleLabel : (object)DBNull.Value;
                        db.AddParameter(cmd, "category_id", DbTypes.Types.Integer).Value = dto.CategoryId;
                        db.AddParameter(cmd, "module_id", DbTypes.Types.Integer).Value =
                            dto.ModuleId.HasValue && dto.ModuleId.Value > 0 ? dto.ModuleId.Value : (object)DBNull.Value;

                        int newId = 0;
                        using (DbDataReader r = await db.Execute(cmd))
                        {
                            if (await r.ReadAsync())
                                newId = r.GetInt32(r.GetOrdinal("id"));
                        }

                        var ticket = new Ticket
                        {
                            Id = newId,
                            CustomerId = custId,
                            CustomerCode = custCode,
                            LocationId = locationId,
                            Subject = dto.Subject,
                            Description = dto.Description,
                            ContactPerson = string.IsNullOrWhiteSpace(dto.ContactPerson) ? null : dto.ContactPerson.Trim(),
                            ContactMobile = string.IsNullOrWhiteSpace(dto.ContactMobile) ? null : dto.ContactMobile.Trim(),
                            StatusId = statusId,
                            Priority = priority,
                            AssignedTo = dto.AssignedTo,
                            CategoryId = dto.CategoryId,
                            ModuleId = dto.ModuleId,
                            SlaDeadline = DateTime.UtcNow,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedBy = createdBy,
                            ModifiedAt = now,
                            ModifiedBy = AuditUserIds.System,
                            Customer = new Customer { Id = custId }
                        };

                        var actorCreate = ResolveTimelineActor(dto.ChangedByUserId, ticket.AssignedTo);
                        await AddTicketTimelineRowAsync(db, ticket.Id, actorCreate, TimelineTypeSystem, BuildCreateTimelineNotes(ticket));

                        await db.CommitTransaction();

                        var created = Map(ticket);
                        await EnrichTicketsAsync(new List<TicketResponseDto> { created });
                        return new ApiResponse<TicketResponseDto> { Success = true, Data = created };
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }
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
                Ticket? ticket = null;
                int customerId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        var load = db.GetCommand("SELECT * FROM tickets WHERE id=@id AND is_active=true LIMIT 1;");
                        db.AddParameter(load, "id", DbTypes.Types.Integer).Value = id;
                        using (DbDataReader r0 = await db.Execute(load))
                        {
                            if (await r0.ReadAsync())
                                ticket = ReadTicket(r0);
                        }
                        if (ticket == null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<TicketResponseDto> { Success = false, Message = "Ticket not found" };
                        }

                        customerId = await GetCustomerIdByCodeAsync(db, ticket.CustomerCode) ?? 0;
                        ticket.Customer = new Customer { Id = customerId };

                        var before = Snap(ticket);
                        if (dto.StatusId is > 0)
                        {
                            var resolvedId = await ResolveTicketStatusIdAsync(db, dto.StatusId);
                            if (resolvedId is null)
                            {
                                await db.RollbackTransaction();
                                return new ApiResponse<TicketResponseDto> { Success = false, Message = $"Invalid ticket status id: {dto.StatusId}" };
                            }
                            ticket.StatusId = resolvedId.Value;
                        }
                if (!string.IsNullOrWhiteSpace(dto.Priority))
                    ticket.Priority = Enum.Parse<TicketPriority>(dto.Priority, true);
                if (dto.AssignedTo.HasValue) ticket.AssignedTo = dto.AssignedTo.Value;

                if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                {
                    var res = await ResolveCustomerLinkAsync(db, 0, dto.CustomerCode);
                    if (res.Error != null)
                    {
                        await db.RollbackTransaction();
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = res.Error };
                    }
                    ticket.CustomerCode = res.CustomerCode;
                    ticket.Customer = new Customer { Id = res.CustomerId };
                    customerId = res.CustomerId;
                }
                else if (dto.CustomerId.HasValue)
                {
                    var res = await ResolveCustomerLinkAsync(db, dto.CustomerId.Value, null);
                    if (res.Error != null)
                    {
                        await db.RollbackTransaction();
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = res.Error };
                    }
                    ticket.CustomerCode = res.CustomerCode;
                    ticket.Customer = new Customer { Id = res.CustomerId };
                    customerId = res.CustomerId;
                }

                if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                {
                    var (lid, lErr) = await ResolveRequiredLocationIdAsync(db, ticket.CustomerCode, 0, dto.LocationCode);
                    if (lErr != null)
                    {
                        await db.RollbackTransaction();
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = lErr };
                    }
                    ticket.LocationId = lid;
                }
                else if (dto.LocationId.HasValue)
                    ticket.LocationId = dto.LocationId.Value;
                if (dto.ContactPerson != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.ContactPerson))
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = "Contact person is required" };
                    ticket.ContactPerson = dto.ContactPerson.Trim();
                }
                if (dto.ContactMobile != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.ContactMobile))
                        return new ApiResponse<TicketResponseDto> { Success = false, Message = "Contact mobile is required" };
                    ticket.ContactMobile = dto.ContactMobile.Trim();
                }

                if (dto.Subject != null) ticket.Subject = dto.Subject.Trim();
                if (dto.Description != null) ticket.Description = dto.Description;
                if (dto.CategoryId.HasValue && dto.CategoryId.Value > 0) ticket.CategoryId = dto.CategoryId.Value;
                if (dto.ModuleId != null) ticket.ModuleId = dto.ModuleId.Value > 0 ? dto.ModuleId.Value : null;
                if (dto.IsActive.HasValue) ticket.IsActive = dto.IsActive.Value;

                // Closure stamps: explicit `closed` always refreshes closed_at/closed_by (e.g. after resolved).
                // First move to `resolved` from active also stamps; reopening clears.
                        var closedId = await ResolveTicketStatusIdByValueAsync(db, "closed") ?? 0;
                        if (closedId > 0 && ticket.StatusId == closedId && before.StatusId != closedId)
                {
                    ticket.ClosedAt = DateTime.UtcNow;
                    ticket.ClosedBy = ClosureActorUserId(dto.ChangedByUserId);
                }
                        else if (closedId > 0 && ticket.StatusId != closedId && before.StatusId == closedId)
                {
                    ticket.ClosedAt = null;
                    ticket.ClosedBy = null;
                }

                ticket.ModifiedAt = DateTime.UtcNow;
                ticket.ModifiedBy = AuditUserIds.System;
                ticket.CustomerId = ticket.Customer?.Id ?? ticket.CustomerId;
                var after = Snap(ticket);
                var updateNotes = BuildUpdateTimelineNotes(before, after);
                if (updateNotes != null)
                {
                    var actorUpdate = ResolveTimelineActor(dto.ChangedByUserId, ticket.AssignedTo);
                            await AddTicketTimelineRowAsync(db, ticket.Id, actorUpdate, TimelineTypeFieldUpdate, updateNotes);
                }

                        var upd = db.GetCommand(@"
UPDATE tickets SET
    status_id=@status_id,
    priority=@priority::ticket_priority,
    assigned_to=@assigned_to,
    customer_id=@customer_id,
    customer_code=@customer_code,
    location_id=@location_id,
    subject=@subject,
    description=@description,
    category=@category,
    module=@module,
    category_id=@category_id,
    module_id=@module_id,
    is_active=@is_active,
    closed_at=@closed_at,
    closed_by=@closed_by,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                        db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = ticket.Id;
                        db.AddParameter(upd, "status_id", DbTypes.Types.Integer).Value = ticket.StatusId;
                        db.AddParameter(upd, "priority", DbTypes.Types.String).Value = ticket.Priority.ToString();
                        db.AddParameter(upd, "assigned_to", DbTypes.Types.Integer).Value = ticket.AssignedTo;
                        db.AddParameter(upd, "customer_id", DbTypes.Types.Integer).Value = ticket.CustomerId;
                        db.AddParameter(upd, "customer_code", DbTypes.Types.String).Value = ticket.CustomerCode;
                        db.AddParameter(upd, "location_id", DbTypes.Types.Integer).Value = ticket.LocationId;
                        db.AddParameter(upd, "subject", DbTypes.Types.String).Value = ticket.Subject;
                        db.AddParameter(upd, "description", DbTypes.Types.String).Value = ticket.Description;
                        var catLabel2 = await ResolveReferenceLabelByIdAsync(db, ticket.CategoryId, "Ticket Category") ?? "General";
                        var modLabel2 = ticket.ModuleId.HasValue && ticket.ModuleId.Value > 0
                            ? await ResolveReferenceLabelByIdAsync(db, ticket.ModuleId.Value, "Ticket Module")
                            : null;
                        db.AddParameter(upd, "category", DbTypes.Types.String).Value = catLabel2;
                        db.AddParameter(upd, "module", DbTypes.Types.String).Value = modLabel2 != null ? modLabel2 : (object)DBNull.Value;
                        db.AddParameter(upd, "category_id", DbTypes.Types.Integer).Value = ticket.CategoryId;
                        db.AddParameter(upd, "module_id", DbTypes.Types.Integer).Value = ticket.ModuleId.HasValue ? ticket.ModuleId.Value : (object)DBNull.Value;
                        db.AddParameter(upd, "is_active", DbTypes.Types.Boolean).Value = ticket.IsActive;
                        db.AddParameter(upd, "closed_at", DbTypes.Types.DateTime).Value = ticket.ClosedAt.HasValue ? ticket.ClosedAt.Value : DBNull.Value;
                        db.AddParameter(upd, "closed_by", DbTypes.Types.Long).Value = ticket.ClosedBy.HasValue ? ticket.ClosedBy.Value : DBNull.Value;
                        db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = ticket.ModifiedAt;
                        db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                        await db.ExecuteNonQuery(upd);

                        await db.CommitTransaction();
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }
                var updated = Map(ticket);
                await EnrichTicketsAsync(new List<TicketResponseDto> { updated });
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
UPDATE tickets
SET is_active=false,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id
RETURNING id;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = DateTime.UtcNow;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (!await r.ReadAsync())
                            return new ApiResponse<bool> { Success = false, Message = "Ticket not found" };
                    }
                    return new ApiResponse<bool> { Success = true, Data = true };
                }
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
                var rows = new List<TicketTimelineEntryDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT id, ticket_id, user_id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by
FROM ticket_timelines
WHERE ticket_id=@tid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "tid", DbTypes.Types.Integer).Value = ticketId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            rows.Add(new TicketTimelineEntryDto
                            {
                                Id = r.GetInt32(r.GetOrdinal("id")),
                                TicketId = r.GetInt32(r.GetOrdinal("ticket_id")),
                                UserId = r.GetInt32(r.GetOrdinal("user_id")),
                                Type = r.GetInt32(r.GetOrdinal("type")),
                                Notes = r.GetString(r.GetOrdinal("notes")),
                                FileId = r.IsDBNull(r.GetOrdinal("file_id")) ? null : r.GetInt32(r.GetOrdinal("file_id")),
                                FileName = r.IsDBNull(r.GetOrdinal("file_name")) ? null : r.GetString(r.GetOrdinal("file_name")),
                                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                                CreatedBy = r.GetInt64(r.GetOrdinal("created_by")),
                                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
                            });
                        }
                    }
                }
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
                var now = DateTime.UtcNow;
                int newId = 0;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var chk = db.GetCommand("SELECT 1 FROM tickets WHERE id=@id AND is_active=true LIMIT 1;");
                    db.AddParameter(chk, "id", DbTypes.Types.Integer).Value = ticketId;
                    using (DbDataReader r0 = await db.Execute(chk))
                    {
                        if (!await r0.ReadAsync())
                            return new ApiResponse<TicketTimelineEntryDto> { Success = false, Message = "Ticket not found" };
                    }

                    var cmd = db.GetCommand(@"
INSERT INTO ticket_timelines (
    ticket_id, user_id, type, notes, file_id, file_name,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @ticket_id, @user_id, @type, @notes, @file_id, @file_name,
    true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                    db.AddParameter(cmd, "ticket_id", DbTypes.Types.Integer).Value = ticketId;
                    db.AddParameter(cmd, "user_id", DbTypes.Types.Integer).Value = dto.UserId;
                    db.AddParameter(cmd, "type", DbTypes.Types.Integer).Value = dto.Type;
                    db.AddParameter(cmd, "notes", DbTypes.Types.String).Value = dto.Notes;
                    db.AddParameter(cmd, "file_id", DbTypes.Types.Integer).Value = dto.FileId.HasValue ? dto.FileId.Value : DBNull.Value;
                    db.AddParameter(cmd, "file_name", DbTypes.Types.String).Value = dto.FileName ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = dto.UserId > 0 ? dto.UserId : AuditUserIds.System;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            newId = r.GetInt32(r.GetOrdinal("id"));
                    }
                }
                return new ApiResponse<TicketTimelineEntryDto>
                {
                    Success = true,
                    Data = new TicketTimelineEntryDto
                    {
                        Id = newId,
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
