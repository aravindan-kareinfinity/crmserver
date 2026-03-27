using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>InvoiceService.ts</c>.</summary>
    public interface IInvoiceService
    {
        Task<ApiResponse<PaginatedResponse<InvoiceResponseDto>>> GetAllInvoices(int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<InvoiceResponseDto>>> GetAll();
        Task<ApiResponse<InvoiceResponseDto>> GetById(int id);
        Task<ApiResponse<List<InvoiceResponseDto>>> GetInvoicesByCustomer(int customerId);
        Task<ApiResponse<List<InvoiceResponseDto>>> GetByStaffId(int staffId);
        Task<ApiResponse<InvoiceResponseDto>> CreateInvoice(CreateInvoiceDto dto);
        Task<ApiResponse<InvoiceResponseDto>> UpdateInvoice(int id, UpdateInvoiceDto dto);
        Task<ApiResponse<bool>> DeleteInvoice(int id);
        Task<ApiResponse<List<InvoiceTimelineEntryDto>>> GetTimeline(int invoiceId);
        Task<ApiResponse<InvoiceTimelineEntryDto>> AddTimelineEntry(int invoiceId, AddTimelineEntryDto dto);
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly CrmDbContext _context;

        public InvoiceService(CrmDbContext context)
        {
            _context = context;
        }

        /// <summary>Uses <see cref="Service.CreatedBy"/> as <see cref="Invoice.StaffId"/> when that row exists in <c>users</c>.</summary>
        private async Task<int?> ResolveStaffIdFromServiceCreatedByAsync(long? serviceCreatedBy)
        {
            if (serviceCreatedBy is null || serviceCreatedBy <= 0 || serviceCreatedBy > int.MaxValue)
                return null;
            var uid = (int)serviceCreatedBy;
            return await _context.Users.AsNoTracking().AnyAsync(u => u.Id == uid) ? uid : null;
        }

        private static string? FormatUserDisplayName(User u)
        {
            var n = $"{u.FirstName} {u.LastName}".Trim();
            if (!string.IsNullOrEmpty(n)) return n;
            return string.IsNullOrEmpty(u.Email) ? null : u.Email;
        }

        private async Task<IReadOnlyDictionary<long, string>> ResolveUserDisplayNamesByIdAsync(IEnumerable<long?> userIds)
        {
            var intIds = userIds
                .Where(id => id.HasValue && id.Value > 0 && id.Value <= int.MaxValue)
                .Select(id => (int)id!.Value)
                .Distinct()
                .ToList();
            if (intIds.Count == 0)
                return new Dictionary<long, string>();

            var users = await _context.Users.AsNoTracking()
                .Where(u => intIds.Contains(u.Id))
                .ToListAsync();

            var dict = new Dictionary<long, string>();
            foreach (var u in users)
            {
                var label = FormatUserDisplayName(u);
                dict[(long)u.Id] = string.IsNullOrEmpty(label) ? $"User #{u.Id}" : label;
            }

            return dict;
        }

        private InvoiceResponseDto MapInvoice(Invoice i, IReadOnlyDictionary<long, string>? createdByLookup = null)
        {
            string? createdByName = null;
            if (i.CreatedBy is long cid && cid > 0 && createdByLookup != null && createdByLookup.TryGetValue(cid, out var resolved))
                createdByName = resolved;

            return new InvoiceResponseDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                CustomerId = i.CustomerId,
                ServiceId = i.ServiceId,
                StaffId = i.StaffId,
                PaymentModeId = i.PaymentModeId,
                PaymentStatusId = i.PaymentStatusId,
                Receivable = i.Receivable,
                Received = i.Received,
                SubscriptionStartAt = i.SubscriptionStartAt,
                SubscriptionEndAt = i.SubscriptionEndAt,
                IsActive = i.IsActive,
                CreatedAt = i.CreatedAt,
                CreatedBy = i.CreatedBy,
                CreatedByName = createdByName,
                ModifiedAt = i.ModifiedAt,
                ModifiedBy = i.ModifiedBy,
                PaidAt = i.PaidAt,
                PaidBy = i.PaidBy
            };
        }

        private async Task AttachTimelineCreatedByNamesAsync(List<InvoiceTimelineEntryDto> rows)
        {
            if (rows.Count == 0) return;
            var lookup = await ResolveUserDisplayNamesByIdAsync(rows.Select(r => (long?)r.CreatedBy));
            foreach (var row in rows)
            {
                if (lookup.TryGetValue(row.CreatedBy, out var name))
                    row.CreatedByName = name;
            }
        }

        public async Task<ApiResponse<PaginatedResponse<InvoiceResponseDto>>> GetAllInvoices(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var total = await _context.Invoices.CountAsync();
                var items = await _context.Invoices.OrderByDescending(i => i.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(items.Select(i => i.CreatedBy));
                return new ApiResponse<PaginatedResponse<InvoiceResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<InvoiceResponseDto>
                    {
                        Items = items.Select(i => MapInvoice(i, createdByLookup)).ToList(),
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceResponseDto>>> GetAll()
        {
            try
            {
                var list = await _context.Invoices.OrderByDescending(i => i.CreatedAt).ToListAsync();
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(list.Select(i => i.CreatedBy));
                return new ApiResponse<List<InvoiceResponseDto>> { Success = true, Data = list.Select(i => MapInvoice(i, createdByLookup)).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceResponseDto>> GetById(int id)
        {
            try
            {
                var i = await _context.Invoices.FindAsync(id);
                if (i == null) return new ApiResponse<InvoiceResponseDto> { Success = false, Message = "Invoice not found" };
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(new[] { i.CreatedBy });
                return new ApiResponse<InvoiceResponseDto> { Success = true, Data = MapInvoice(i, createdByLookup) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvoiceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceResponseDto>>> GetInvoicesByCustomer(int customerId)
        {
            try
            {
                var list = await _context.Invoices.Where(i => i.CustomerId == customerId).OrderByDescending(i => i.CreatedAt).ToListAsync();
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(list.Select(i => i.CreatedBy));
                return new ApiResponse<List<InvoiceResponseDto>> { Success = true, Data = list.Select(i => MapInvoice(i, createdByLookup)).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceResponseDto>>> GetByStaffId(int staffId)
        {
            try
            {
                var list = await _context.Invoices.Where(i => i.StaffId == staffId).OrderByDescending(i => i.CreatedAt).ToListAsync();
                var createdByLookup = await ResolveUserDisplayNamesByIdAsync(list.Select(i => i.CreatedBy));
                return new ApiResponse<List<InvoiceResponseDto>> { Success = true, Data = list.Select(i => MapInvoice(i, createdByLookup)).ToList() };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceResponseDto>> CreateInvoice(CreateInvoiceDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                var staffId = dto.StaffId;
                if (!staffId.HasValue && dto.ServiceId > 0)
                {
                    var svc = await _context.Services.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == dto.ServiceId);
                    if (svc != null)
                        staffId = await ResolveStaffIdFromServiceCreatedByAsync(svc.CreatedBy);
                }

                var invoice = new Invoice
                {
                    InvoiceNumber = dto.InvoiceNumber,
                    CustomerId = dto.CustomerId,
                    ServiceId = dto.ServiceId,
                    StaffId = staffId,
                    PaymentModeId = dto.PaymentModeId,
                    PaymentStatusId = dto.PaymentStatusId,
                    Receivable = dto.Receivable,
                    Received = dto.Received,
                    SubscriptionStartAt = dto.SubscriptionStartAt,
                    SubscriptionEndAt = dto.SubscriptionEndAt,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();
                var createLookup = await ResolveUserDisplayNamesByIdAsync(new[] { invoice.CreatedBy });
                return new ApiResponse<InvoiceResponseDto> { Success = true, Data = MapInvoice(invoice, createLookup) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvoiceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceResponseDto>> UpdateInvoice(int id, UpdateInvoiceDto dto)
        {
            try
            {
                var i = await _context.Invoices.FindAsync(id);
                if (i == null) return new ApiResponse<InvoiceResponseDto> { Success = false, Message = "Invoice not found" };
                if (dto.PaymentStatusId.HasValue) i.PaymentStatusId = dto.PaymentStatusId.Value;
                if (dto.Receivable.HasValue) i.Receivable = dto.Receivable.Value;
                if (dto.Received.HasValue) i.Received = dto.Received.Value;
                if (dto.SubscriptionStartAt.HasValue) i.SubscriptionStartAt = dto.SubscriptionStartAt.Value;
                if (dto.SubscriptionEndAt.HasValue) i.SubscriptionEndAt = dto.SubscriptionEndAt.Value;
                if (dto.IsActive.HasValue) i.IsActive = dto.IsActive.Value;
                if (dto.PaidAt.HasValue) i.PaidAt = dto.PaidAt;
                if (dto.PaidBy != null) i.PaidBy = dto.PaidBy;
                i.ModifiedAt = DateTime.UtcNow;
                i.ModifiedBy = AuditUserIds.System;
                await _context.SaveChangesAsync();
                var updateLookup = await ResolveUserDisplayNamesByIdAsync(new[] { i.CreatedBy });
                return new ApiResponse<InvoiceResponseDto> { Success = true, Data = MapInvoice(i, updateLookup) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvoiceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteInvoice(int id)
        {
            try
            {
                var i = await _context.Invoices.FindAsync(id);
                if (i == null) return new ApiResponse<bool> { Success = false, Message = "Invoice not found" };
                _context.Invoices.Remove(i);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvoiceTimelineEntryDto>>> GetTimeline(int invoiceId)
        {
            try
            {
                var rows = await _context.InvoiceTimelines.Where(t => t.InvoiceId == invoiceId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new InvoiceTimelineEntryDto
                    {
                        Id = t.Id,
                        InvoiceId = t.InvoiceId,
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
                await AttachTimelineCreatedByNamesAsync(rows);
                return new ApiResponse<List<InvoiceTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvoiceTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvoiceTimelineEntryDto>> AddTimelineEntry(int invoiceId, AddTimelineEntryDto dto)
        {
            try
            {
                var inv = await _context.Invoices.FindAsync(invoiceId);
                if (inv == null) return new ApiResponse<InvoiceTimelineEntryDto> { Success = false, Message = "Invoice not found" };
                var now = DateTime.UtcNow;
                var e = new InvoiceTimeline
                {
                    InvoiceId = invoiceId,
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
                _context.InvoiceTimelines.Add(e);
                await _context.SaveChangesAsync();
                return new ApiResponse<InvoiceTimelineEntryDto>
                {
                    Success = true,
                    Data = new InvoiceTimelineEntryDto
                    {
                        Id = e.Id,
                        InvoiceId = e.InvoiceId,
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
                return new ApiResponse<InvoiceTimelineEntryDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
