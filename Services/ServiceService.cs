using System.Globalization;
using System.Text.RegularExpressions;
using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRM.Server.Services
{
    public interface IServiceService
    {
        Task<ApiResponse<PaginatedResponse<ServiceResponseDto>>> GetAllServices(int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<ServiceResponseDto>>> GetAllServicesList();
        Task<ApiResponse<ServiceResponseDto>> GetServiceById(int id);
        Task<ApiResponse<List<ServiceResponseDto>>> GetServicesByCustomer(int customerId);
        Task<ApiResponse<List<ServiceResponseDto>>> GetServicesByCustomerCode(string customerCode);
        Task<ApiResponse<ServiceResponseDto>> CreateService(CreateServiceDto dto);
        Task<ApiResponse<ServiceResponseDto>> UpdateService(int id, UpdateServiceDto dto);
        Task<ApiResponse<bool>> DeleteService(int id);
        Task<ApiResponse<List<ImplementationTimelineEntryDto>>> GetImplementationTimeline(int serviceId);
        Task<ApiResponse<ImplementationTimelineEntryDto>> AddImplementationTimelineEntry(int serviceId, AddImplementationTimelineEntryDto dto);
        Task<ApiResponse<List<ImplementationAssignmentDto>>> GetAllImplementationAssignments();
        Task<ApiResponse<ImplementationAssignmentDto>> UpsertImplementationAssignment(int serviceId, UpsertImplementationAssignmentDto dto);
        Task<ApiResponse<ServiceResponseDto>> GoLive(int id, GoLiveServiceDto dto);
    }

    public class ServiceService : IServiceService
    {
        private readonly CrmDbContext _context;

        public ServiceService(CrmDbContext context)
        {
            _context = context;
        }

        /// <summary>Surfaces PostgreSQL errors hidden inside EF <see cref="DbUpdateException"/>.</summary>
        private static string FormatPersistenceError(Exception ex)
        {
            for (Exception? cur = ex; cur != null; cur = cur.InnerException)
            {
                if (cur is PostgresException pg)
                {
                    if (!string.IsNullOrWhiteSpace(pg.Detail))
                        return $"{pg.Message} — {pg.Detail}";
                    return pg.Message;
                }
            }

            return ex.Message;
        }

        /// <summary>Uses <see cref="Service.CreatedBy"/> as invoice <c>staff_id</c> when that user exists.</summary>
        private async Task<int?> ResolveStaffIdFromServiceCreatedByAsync(long? serviceCreatedBy)
        {
            if (serviceCreatedBy is null || serviceCreatedBy <= 0 || serviceCreatedBy > int.MaxValue)
                return null;
            var uid = (int)serviceCreatedBy;
            return await _context.Users.AsNoTracking().AnyAsync(u => u.Id == uid) ? uid : null;
        }

        private static ServiceResponseDto MapService(Service s) => new()
        {
            Id = s.Id,
            CustomerId = s.Customer?.Id ?? 0,
            LocationId = s.LocationId,
            TradeNameId = s.TradeNameId,
            ServiceTypeId = s.ServiceTypeId,
            FrequencyId = s.FrequencyId,
            DueDate = s.DueDate,
            DueMonth = s.DueMonth,
            AmcPercentage = s.AmcPercentage,
            AmcAmount = s.AmcAmount,
            ImplementationRequired = s.ImplementationRequired,
            ImplementationStatusId = WorkflowToApiCode(s.ImplementationStatus),
            ImplementationStageId = s.ImplementationStageId,
            ImplementationStartedAt = s.ImplementationStartedAt,
            ImplementationStartedBy = s.ImplementationStartedBy,
            ImplementationCompletedAt = s.ImplementationCompletedAt,
            ImplementationCompletedBy = s.ImplementationCompletedBy,
            ProjectTitle = s.ProjectTitle,
            ProjectManagerId = s.ProjectManagerId,
            ProgressPercentage = s.ProgressPercentage,
            ServiceValue = s.ServiceValue,
            TaxId = s.TaxId,
            Notes = s.Notes,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy,
            ModifiedAt = s.ModifiedAt,
            ModifiedBy = s.ModifiedBy,
            LiveDate = s.LiveDate
        };

        /// <summary>Hardcoded API codes for implementation status (not reference_entries).</summary>
        private static int WorkflowToApiCode(ImplementationWorkflowStatus w) => w switch
        {
            ImplementationWorkflowStatus.IN_PROGRESS => 2,
            ImplementationWorkflowStatus.COMPLETED => 3,
            ImplementationWorkflowStatus.OPEN => 1,
            _ => 1
        };

        private static ImplementationWorkflowStatus ApiCodeToWorkflow(int code) => code switch
        {
            2 => ImplementationWorkflowStatus.IN_PROGRESS,
            3 => ImplementationWorkflowStatus.COMPLETED,
            _ => ImplementationWorkflowStatus.OPEN
        };

        /// <summary>
        /// Sets implementation_started_* / implementation_completed_* when workflow advances (user ids stored as strings).
        /// </summary>
        private static void ApplyImplementationWorkflowTransition(
            Service service,
            ImplementationWorkflowStatus previous,
            ImplementationWorkflowStatus next,
            DateTime utcNow,
            long actorUserId)
        {
            var idStr = actorUserId.ToString(CultureInfo.InvariantCulture);

            if (next == ImplementationWorkflowStatus.IN_PROGRESS &&
                previous != ImplementationWorkflowStatus.IN_PROGRESS &&
                service.ImplementationStartedAt == null)
            {
                service.ImplementationStartedAt = utcNow;
                service.ImplementationStartedBy = idStr;
            }

            if (next == ImplementationWorkflowStatus.COMPLETED &&
                previous != ImplementationWorkflowStatus.COMPLETED)
            {
                service.ImplementationCompletedAt = utcNow;
                service.ImplementationCompletedBy = idStr;
            }
        }

        private static ImplementationTimelineEntryDto MapImplementationTimeline(ImplementationTimeline x) => new()
        {
            Id = x.Id,
            ServiceId = x.ServiceId,
            Type = x.Type,
            StatusId = WorkflowToApiCode(x.WorkflowStatus),
            Status = x.WorkflowStatus.ToString(),
            Notes = x.Notes,
            FileId = x.FileId,
            FileName = x.FileName,
            UserId = x.UserId,
            IsActive = x.IsActive,
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy,
            ModifiedAt = x.ModifiedAt,
            ModifiedBy = x.ModifiedBy
        };

        /// <summary>
        /// Derives GST % from <c>reference_entries</c> (seed uses Value like <c>gst_18</c>, <c>no_tax</c>).
        /// </summary>
        private static decimal ResolveTaxPercent(ReferenceEntry? tax)
        {
            if (tax == null) return 0;
            var v = (tax.Value ?? "").Trim();
            if (string.Equals(v, "no_tax", StringComparison.OrdinalIgnoreCase)) return 0;
            if (v.StartsWith("gst_", StringComparison.OrdinalIgnoreCase) && v.Length > 4)
            {
                var suffix = v[4..].Replace('_', '.');
                if (decimal.TryParse(suffix, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                    return p;
            }

            var m = Regex.Match(tax.Label ?? "", @"(\d+(?:\.\d+)?)\s*%");
            if (m.Success &&
                decimal.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var labelPct))
                return labelPct;
            return 0;
        }

        private static decimal ComputeReceivable(decimal baseAmount, decimal taxPercent) =>
            Math.Round(baseAmount + baseAmount * (taxPercent / 100m), 2, MidpointRounding.AwayFromZero);

        private async Task<(int PaymentModeId, int PaymentStatusId)> GetDefaultInvoicePaymentRefsAsync()
        {
            var mode = await _context.ReferenceEntries.AsNoTracking()
                .Where(r => r.Category == "Payment Mode" && r.IsActive)
                .OrderBy(r => r.SortOrder)
                .FirstOrDefaultAsync();
            var pending = await _context.ReferenceEntries.AsNoTracking()
                .Where(r => r.Category == "Payment Status" && r.IsActive &&
                            r.Value.ToLower() == "pending")
                .OrderBy(r => r.SortOrder)
                .FirstOrDefaultAsync()
                ?? await _context.ReferenceEntries.AsNoTracking()
                    .Where(r => r.Category == "Payment Status" && r.IsActive)
                    .OrderBy(r => r.SortOrder)
                    .FirstOrDefaultAsync();

            if (mode == null || pending == null)
                throw new InvalidOperationException(
                    "Reference data missing: ensure 'Payment Mode' and 'Payment Status' rows exist.");

            return (mode.Id, pending.Id);
        }

        /// <summary>
        /// Creates or updates the primary invoice for this service: receivable = base amount + GST.
        /// Removes unpaid invoices when the service no longer has a positive base amount.
        /// </summary>
        private async Task SyncBillingInvoiceForServiceAsync(Service service)
        {
            var baseAmount = service.ServiceValue ?? 0;
            if (baseAmount <= 0)
            {
                var removable = await _context.Invoices
                    .Where(i => i.ServiceId == service.Id && i.Received <= 0)
                    .ToListAsync();
                foreach (var inv in removable)
                    _context.Invoices.Remove(inv);
                return;
            }

            ReferenceEntry? taxEntry = null;
            if (service.TaxId.HasValue)
            {
                taxEntry = await _context.ReferenceEntries.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == service.TaxId.Value);
            }

            var taxPct = ResolveTaxPercent(taxEntry);
            var receivable = ComputeReceivable(baseAmount, taxPct);
            var now = DateTime.UtcNow;

            var invoice = await _context.Invoices
                .Where(i => i.ServiceId == service.Id && !i.InvoiceNumber.StartsWith("INV-AMC-"))
                .OrderByDescending(i => i.Id)
                .FirstOrDefaultAsync();

            // IMPORTANT: Do NOT bind subscription dates unless the service is explicitly marked live.
            // When LiveDate is not set, keep invoice dates at +/-infinity.
            // Npgsql represents +/-infinity as DateTime.MinValue/MaxValue; API normalizes those to null for UI.
            var startAt = service.LiveDate.HasValue ? service.LiveDate.Value.Date : DateTime.MinValue;
            var endAt = service.LiveDate.HasValue ? startAt.AddYears(1) : DateTime.MaxValue;
            if (service.LiveDate.HasValue && service.FrequencyId.HasValue)
            {
                var freq = await _context.ReferenceEntries.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == service.FrequencyId.Value);
                if (freq != null && !string.IsNullOrEmpty(freq.Label))
                {
                    var label = freq.Label.ToLowerInvariant();
                    if (label.Contains("month")) endAt = startAt.AddMonths(1);
                    else if (label.Contains("quarter")) endAt = startAt.AddMonths(3);
                    else if (label.Contains("half")) endAt = startAt.AddMonths(6);
                }
            }

            if (invoice == null)
            {
                var (modeId, statusId) = await GetDefaultInvoicePaymentRefsAsync();
                var staffFromServiceCreator = await ResolveStaffIdFromServiceCreatedByAsync(service.CreatedBy);
                invoice = new Invoice
                {
                    InvoiceNumber = $"INV-S{service.Id}-{now:yyyyMMddHHmmss}",
                    CustomerCode = service.CustomerCode,
                    ServiceId = service.Id,
                    StaffId = staffFromServiceCreator,
                    PaymentModeId = modeId,
                    PaymentStatusId = statusId,
                    Receivable = receivable,
                    Received = 0,
                    SubscriptionStartAt = startAt,
                    SubscriptionEndAt = endAt,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Invoices.Add(invoice);
            }
            else
            {
                invoice.CustomerCode = service.CustomerCode;
                invoice.Receivable = receivable;
                invoice.SubscriptionStartAt = startAt;
                invoice.SubscriptionEndAt = endAt;
                invoice.ModifiedAt = now;
                invoice.ModifiedBy = AuditUserIds.System;
            }
        }

        private async Task EnsureAmcInvoiceAsync(Service service, DateTime currentStartAt, DateTime currentEndAt)
        {
            var amcAmount = service.AmcAmount;
            if (!amcAmount.HasValue && service.AmcPercentage.HasValue && (service.ServiceValue ?? 0) > 0)
            {
                amcAmount = Math.Round((service.ServiceValue!.Value * (service.AmcPercentage.Value / 100m)), 2, MidpointRounding.AwayFromZero);
            }

            if (!amcAmount.HasValue || amcAmount.Value <= 0)
                return;

            var nextStart = currentEndAt.Date;
            var nextEnd = nextStart.AddYears(1);

            // Resolve "AMC" service type id from reference data (category: Service Type, value: amc).
            var amcTypeId = await _context.ReferenceEntries.AsNoTracking()
                .Where(r => r.Category == "Service Type" && r.IsActive && r.Value.ToLower() == "amc")
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync();
            if (!amcTypeId.HasValue)
                return;

            // Ensure there is an AMC service row to bind this invoice.
            var amcService = await _context.Services
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync(s =>
                    s.CustomerCode == service.CustomerCode &&
                    s.ServiceTypeId == amcTypeId.Value &&
                    s.LiveDate.HasValue &&
                    s.LiveDate.Value.Date == nextStart);

            if (amcService == null)
            {
                var now = DateTime.UtcNow;
                amcService = new Service
                {
                    CustomerCode = service.CustomerCode,
                    LocationId = service.LocationId,
                    TradeNameId = service.TradeNameId,
                    ServiceTypeId = amcTypeId.Value,
                    FrequencyId = service.FrequencyId,
                    DueDate = nextStart,
                    DueMonth = nextStart.Month,
                    ImplementationRequired = false,
                    ImplementationStatus = ImplementationWorkflowStatus.OPEN,
                    TaxId = service.TaxId,
                    ServiceValue = amcAmount.Value,
                    AmcPercentage = null,
                    AmcAmount = null,
                    Notes = $"AMC for service #{service.Id}",
                    IsActive = true,
                    LiveDate = nextStart,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System,
                    ProgressPercentage = 0
                };
                _context.Services.Add(amcService);
                await _context.SaveChangesAsync();
            }

            // Avoid duplicate AMC invoices for this AMC service + period.
            var exists = await _context.Invoices.AsNoTracking().AnyAsync(i =>
                i.ServiceId == amcService.Id &&
                i.InvoiceNumber.StartsWith("INV-AMC-") &&
                i.SubscriptionStartAt == nextStart &&
                i.SubscriptionEndAt == nextEnd);
            if (exists) return;

            ReferenceEntry? taxEntry = null;
            if (service.TaxId.HasValue)
            {
                taxEntry = await _context.ReferenceEntries.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == service.TaxId.Value);
            }
            var taxPct = ResolveTaxPercent(taxEntry);
            var receivable = ComputeReceivable(amcAmount.Value, taxPct);

            var now2 = DateTime.UtcNow;
            var (modeId, statusId) = await GetDefaultInvoicePaymentRefsAsync();
            var staffFromServiceCreator = await ResolveStaffIdFromServiceCreatedByAsync(service.CreatedBy);

            var inv = new Invoice
            {
                InvoiceNumber = $"INV-AMC-S{amcService.Id}-{now2:yyyyMMddHHmmss}",
                CustomerCode = service.CustomerCode,
                ServiceId = amcService.Id,
                StaffId = staffFromServiceCreator,
                PaymentModeId = modeId,
                PaymentStatusId = statusId,
                Receivable = receivable,
                Received = 0,
                SubscriptionStartAt = nextStart,
                SubscriptionEndAt = nextEnd,
                IsActive = true,
                CreatedAt = now2,
                CreatedBy = AuditUserIds.System,
                ModifiedAt = now2,
                ModifiedBy = AuditUserIds.System
            };
            _context.Invoices.Add(inv);
        }

        public async Task<ApiResponse<ServiceResponseDto>> GoLive(int id, GoLiveServiceDto dto)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id);
                if (service == null)
                {
                    await tx.RollbackAsync();
                    return new ApiResponse<ServiceResponseDto> { Success = false, Message = "Service not found" };
                }

                service.LiveDate = dto.LiveDate;
                service.ModifiedAt = DateTime.UtcNow;
                service.ModifiedBy = dto.ModifiedByUserId is { } uid && uid > 0 ? uid : AuditUserIds.System;

                await _context.SaveChangesAsync();
                await SyncBillingInvoiceForServiceAsync(service);
                if (service.LiveDate.HasValue)
                {
                    var currentStart = service.LiveDate.Value.Date;
                    var currentEnd = currentStart.AddYears(1);
                    await EnsureAmcInvoiceAsync(service, currentStart, currentEnd);
                }
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _context.Entry(service).Reference(s => s.Customer).LoadAsync();
                var updated = MapService(service);
                await EntityCodeResolution.EnrichServiceDtosAsync(_context, new List<ServiceResponseDto> { updated });
                return new ApiResponse<ServiceResponseDto> { Success = true, Message = "Service marked live", Data = updated };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ApiResponse<ServiceResponseDto> { Success = false, Message = $"Error marking service live: {FormatPersistenceError(ex)}" };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<ServiceResponseDto>>> GetAllServices(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var total = await _context.Services.CountAsync();
                var services = await _context.Services
                    .AsNoTracking()
                    .Include(s => s.Customer)
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = services.Select(MapService).ToList();
                await EntityCodeResolution.EnrichServiceDtosAsync(_context, items);
                return new ApiResponse<PaginatedResponse<ServiceResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<ServiceResponseDto>
                    {
                        Items = items,
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<ServiceResponseDto>>
                {
                    Success = false,
                    Message = $"Error fetching services: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<List<ServiceResponseDto>>> GetAllServicesList()
        {
            try
            {
                var list = await _context.Services.AsNoTracking().Include(s => s.Customer).OrderByDescending(s => s.CreatedAt).ToListAsync();
                var dtos = list.Select(MapService).ToList();
                await EntityCodeResolution.EnrichServiceDtosAsync(_context, dtos);
                return new ApiResponse<List<ServiceResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ServiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ServiceResponseDto>> GetServiceById(int id)
        {
            try
            {
                var service = await _context.Services.AsNoTracking().Include(s => s.Customer).FirstOrDefaultAsync(s => s.Id == id);
                if (service == null)
                    return new ApiResponse<ServiceResponseDto> { Success = false, Message = "Service not found" };
                var one = MapService(service);
                await EntityCodeResolution.EnrichServiceDtosAsync(_context, new List<ServiceResponseDto> { one });
                return new ApiResponse<ServiceResponseDto> { Success = true, Data = one };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ServiceResponseDto> { Success = false, Message = $"Error fetching service: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<List<ServiceResponseDto>>> GetServicesByCustomer(int customerId)
        {
            try
            {
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(_context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<ServiceResponseDto>> { Success = true, Data = new List<ServiceResponseDto>() };
                var services = await _context.Services.AsNoTracking()
                    .Include(s => s.Customer)
                    .Where(s => s.CustomerCode == cc)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();
                var dtos = services.Select(MapService).ToList();
                await EntityCodeResolution.EnrichServiceDtosAsync(_context, dtos);
                return new ApiResponse<List<ServiceResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ServiceResponseDto>> { Success = false, Message = $"Error fetching services: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<List<ServiceResponseDto>>> GetServicesByCustomerCode(string customerCode)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(_context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<List<ServiceResponseDto>> { Success = false, Message = err };
                return await GetServicesByCustomer(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ServiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ServiceResponseDto>> CreateService(CreateServiceDto dto)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var (custCode, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(_context, dto.CustomerId, dto.CustomerCode);
                if (cErr != null)
                    return new ApiResponse<ServiceResponseDto> { Success = false, Message = cErr };

                var (locId, lErr) = await EntityCodeResolution.ResolveOptionalLocationIdAsync(
                    _context, custCode, dto.LocationId, dto.LocationCode);
                if (lErr != null)
                    return new ApiResponse<ServiceResponseDto> { Success = false, Message = lErr };

                var now = DateTime.UtcNow;
                var dueMonth = dto.DueMonth is > 0 ? dto.DueMonth.Value : dto.DueDate.Month;
                var service = new Service
                {
                    CustomerCode = custCode,
                    LocationId = locId,
                    TradeNameId = dto.TradeNameId,
                    ServiceTypeId = dto.ServiceTypeId,
                    FrequencyId = dto.FrequencyId,
                    DueDate = dto.DueDate,
                    DueMonth = dueMonth,
                    AmcPercentage = dto.AmcPercentage,
                    AmcAmount = dto.AmcAmount,
                    ImplementationRequired = dto.ImplementationRequired,
                    ImplementationStatus = dto.ImplementationStatusId is { } sid
                        ? ApiCodeToWorkflow(sid)
                        : ImplementationWorkflowStatus.OPEN,
                    ProjectTitle = dto.ProjectTitle,
                    ProjectManagerId = dto.ProjectManagerId,
                    BudgetAmount = dto.BudgetAmount,
                    TaxId = dto.TaxId,
                    ServiceValue = dto.ServiceValue,
                    Notes = dto.Notes,
                    IsActive = true,
                    LiveDate = dto.LiveDate,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System,
                    ProgressPercentage = 0
                };

                _context.Services.Add(service);
                await _context.SaveChangesAsync();
                await SyncBillingInvoiceForServiceAsync(service);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _context.Entry(service).Reference(s => s.Customer).LoadAsync();
                var createdDto = MapService(service);
                await EntityCodeResolution.EnrichServiceDtosAsync(_context, new List<ServiceResponseDto> { createdDto });
                return new ApiResponse<ServiceResponseDto>
                {
                    Success = true,
                    Message = "Service created successfully",
                    Data = createdDto
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ApiResponse<ServiceResponseDto> { Success = false, Message = $"Error creating service: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<ServiceResponseDto>> UpdateService(int id, UpdateServiceDto dto)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null)
                {
                    await tx.RollbackAsync();
                    return new ApiResponse<ServiceResponseDto> { Success = false, Message = "Service not found" };
                }

                if (dto.ServiceTypeId.HasValue) service.ServiceTypeId = dto.ServiceTypeId.Value;
                if (dto.FrequencyId.HasValue) service.FrequencyId = dto.FrequencyId;
                if (dto.DueDate.HasValue) service.DueDate = dto.DueDate.Value;
                if (dto.DueMonth.HasValue) service.DueMonth = dto.DueMonth.Value;
                else if (dto.DueDate.HasValue) service.DueMonth = dto.DueDate.Value.Month;
                if (dto.AmcPercentage.HasValue) service.AmcPercentage = dto.AmcPercentage;
                if (dto.AmcAmount.HasValue) service.AmcAmount = dto.AmcAmount;
                if (dto.ImplementationRequired.HasValue) service.ImplementationRequired = dto.ImplementationRequired.Value;
                if (dto.ImplementationStatusId.HasValue)
                {
                    var nextStatus = ApiCodeToWorkflow(dto.ImplementationStatusId.Value);
                    var prevStatus = service.ImplementationStatus;
                    service.ImplementationStatus = nextStatus;
                    ApplyImplementationWorkflowTransition(
                        service,
                        prevStatus,
                        nextStatus,
                        DateTime.UtcNow,
                        dto.ModifiedByUserId ?? 0);
                }

                if (dto.BeginImplementation == true)
                {
                    var uid = dto.ModifiedByUserId ?? 0;
                    var now = DateTime.UtcNow;
                    if (service.ImplementationStartedAt == null)
                    {
                        service.ImplementationStartedAt = now;
                        service.ImplementationStartedBy = uid.ToString(CultureInfo.InvariantCulture);
                    }

                    var prev = service.ImplementationStatus;
                    service.ImplementationStatus = ImplementationWorkflowStatus.OPEN;
                    if (prev != ImplementationWorkflowStatus.OPEN)
                    {
                        ApplyImplementationWorkflowTransition(service, prev, ImplementationWorkflowStatus.OPEN, now, uid);
                    }
                }

                if (dto.IsActive.HasValue) service.IsActive = dto.IsActive.Value;
                if (dto.Notes != null) service.Notes = dto.Notes;

                if (dto.UpdateBillingLinks == true)
                {
                    if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                    {
                        var (lid, lErr) = await EntityCodeResolution.ResolveOptionalLocationIdAsync(
                            _context, service.CustomerCode, null, dto.LocationCode);
                        if (lErr != null)
                        {
                            await tx.RollbackAsync();
                            return new ApiResponse<ServiceResponseDto> { Success = false, Message = lErr };
                        }

                        service.LocationId = lid;
                    }
                    else
                    {
                        service.LocationId = dto.LocationId;
                    }

                    service.TradeNameId = dto.TradeNameId;
                    service.TaxId = dto.TaxId;
                    service.ServiceValue = dto.ServiceValue;
                    if (dto.UpdateBillingLinks == true) 
                    {
                        // Set LiveDate from dto if passed since it usually triggers invoice logic
                        service.LiveDate = dto.LiveDate;
                    }
                }

                if (dto.ProjectManagerId.HasValue)
                    service.ProjectManagerId = dto.ProjectManagerId.Value <= 0 ? null : dto.ProjectManagerId.Value;
                if (dto.ProgressPercentage.HasValue)
                    service.ProgressPercentage = Math.Clamp(dto.ProgressPercentage.Value, 0, 100);

                service.ModifiedAt = DateTime.UtcNow;
                service.ModifiedBy = dto.ModifiedByUserId > 0 ? dto.ModifiedByUserId : AuditUserIds.System;

                await _context.SaveChangesAsync();
                if (dto.UpdateBillingLinks == true)
                {
                    await SyncBillingInvoiceForServiceAsync(service);
                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();
                await _context.Entry(service).Reference(s => s.Customer).LoadAsync();
                var updated = MapService(service);
                await EntityCodeResolution.EnrichServiceDtosAsync(_context, new List<ServiceResponseDto> { updated });
                return new ApiResponse<ServiceResponseDto> { Success = true, Message = "Service updated successfully", Data = updated };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ApiResponse<ServiceResponseDto> { Success = false, Message = $"Error updating service: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteService(int id)
        {
            try
            {
                var service = await _context.Services.FindAsync(id);
                if (service == null)
                    return new ApiResponse<bool> { Success = false, Message = "Service not found" };

                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Message = "Service deleted successfully", Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = $"Error deleting service: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<List<ImplementationTimelineEntryDto>>> GetImplementationTimeline(int serviceId)
        {
            try
            {
                var rows = await _context.ImplementationTimelines.AsNoTracking()
                    .Where(t => t.ServiceId == serviceId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
                return new ApiResponse<List<ImplementationTimelineEntryDto>>
                {
                    Success = true,
                    Data = rows.Select(MapImplementationTimeline).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ImplementationTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ImplementationTimelineEntryDto>> AddImplementationTimelineEntry(
            int serviceId,
            AddImplementationTimelineEntryDto dto)
        {
            try
            {
                var svc = await _context.Services.FindAsync(serviceId);
                if (svc == null)
                    return new ApiResponse<ImplementationTimelineEntryDto> { Success = false, Message = "Service not found" };

                // Session may reference a deleted user, or clients may send userId 0 — fall back to system / first user.
                var user = dto.UserId > 0 ? await _context.Users.FindAsync(dto.UserId) : null;
                if (user == null)
                {
                    user = await _context.Users.FindAsync((int)AuditUserIds.System)
                        ?? await _context.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
                }
                if (user == null)
                    return new ApiResponse<ImplementationTimelineEntryDto>
                    {
                        Success = false,
                        Message = "No matching user for this request; add at least one user to the database."
                    };

                var workflow = ApiCodeToWorkflow(dto.StatusId);

                var now = DateTime.UtcNow;
                var actorId = (long)user.Id;

                var prevSvcStatus = svc.ImplementationStatus;
                svc.ImplementationStatus = workflow;
                ApplyImplementationWorkflowTransition(svc, prevSvcStatus, workflow, now, actorId);
                svc.ModifiedAt = now;
                svc.ModifiedBy = actorId;

                var e = new ImplementationTimeline
                {
                    ServiceId = serviceId,
                    Type = dto.Type,
                    WorkflowStatus = workflow,
                    Notes = dto.Notes ?? string.Empty,
                    FileId = dto.FileId,
                    FileName = dto.FileName,
                    UserId = user.Id,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = actorId,
                    ModifiedAt = now,
                    ModifiedBy = actorId
                };
                _context.ImplementationTimelines.Add(e);
                await _context.SaveChangesAsync();
                return new ApiResponse<ImplementationTimelineEntryDto> { Success = true, Data = MapImplementationTimeline(e) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ImplementationTimelineEntryDto>
                {
                    Success = false,
                    Message = FormatPersistenceError(ex)
                };
            }
        }

        public async Task<ApiResponse<List<ImplementationAssignmentDto>>> GetAllImplementationAssignments()
        {
            try
            {
                var rows = await _context.ImplementationAssignments.AsNoTracking()
                    .OrderBy(a => a.ServiceId)
                    .ToListAsync();
                return new ApiResponse<List<ImplementationAssignmentDto>>
                {
                    Success = true,
                    Data = rows.Select(MapImplementationAssignment).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ImplementationAssignmentDto>> { Success = false, Message = ex.Message };
            }
        }

        private static ImplementationAssignmentDto MapImplementationAssignment(ImplementationAssignment a) => new()
        {
            Id = a.Id,
            ServiceId = a.ServiceId,
            UserIds = a.UserIds ?? new List<int>()
        };

        public async Task<ApiResponse<ImplementationAssignmentDto>> UpsertImplementationAssignment(
            int serviceId,
            UpsertImplementationAssignmentDto dto)
        {
            try
            {
                var svc = await _context.Services.FindAsync(serviceId);
                if (svc == null)
                    return new ApiResponse<ImplementationAssignmentDto> { Success = false, Message = "Service not found" };

                var userIds = dto.UserIds ?? new List<int>();

                var existing = await _context.ImplementationAssignments
                    .Where(a => a.ServiceId == serviceId)
                    .OrderBy(a => a.Id)
                    .ToListAsync();

                ImplementationAssignment entity;
                if (existing.Count == 0)
                {
                    entity = new ImplementationAssignment { ServiceId = serviceId, UserIds = userIds };
                    _context.ImplementationAssignments.Add(entity);
                }
                else
                {
                    entity = existing[0];
                    entity.UserIds = userIds;
                    if (existing.Count > 1)
                        _context.ImplementationAssignments.RemoveRange(existing.Skip(1));
                }

                await _context.SaveChangesAsync();
                return new ApiResponse<ImplementationAssignmentDto>
                {
                    Success = true,
                    Message = "Team assignment saved",
                    Data = MapImplementationAssignment(entity)
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ImplementationAssignmentDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
