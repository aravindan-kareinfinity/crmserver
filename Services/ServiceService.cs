using System.Globalization;
using System.Text.RegularExpressions;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Npgsql;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    public interface IServiceService
    {
        Task<ApiResponse<PaginatedResponse<ServiceResponseDto>>> GetAllServices(int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<ServiceResponseDto>>> GetAllServicesList(ServiceQueryDto q);
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
        IDbProvider dbprovider;

        public ServiceService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        private static List<int> SplitCsvInts(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<int>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList();
        }

        private static string JoinCsvInts(List<int>? ids) =>
            ids == null ? string.Empty : string.Join(",", ids.Where(x => x > 0).Distinct());

        private static ImplementationWorkflowStatus ParseWorkflowStatus(string raw) =>
            raw?.Trim().ToUpperInvariant() switch
            {
                "IN_PROGRESS" => ImplementationWorkflowStatus.IN_PROGRESS,
                "COMPLETED" => ImplementationWorkflowStatus.COMPLETED,
                _ => ImplementationWorkflowStatus.OPEN
            };

        private static string WorkflowToDb(ImplementationWorkflowStatus w) => w switch
        {
            ImplementationWorkflowStatus.IN_PROGRESS => "IN_PROGRESS",
            ImplementationWorkflowStatus.COMPLETED => "COMPLETED",
            _ => "OPEN"
        };

        private static Service ReadService(DbDataReader r)
        {
            return new Service
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                CustomerId = r.GetInt32(r.GetOrdinal("customer_id")),
                CustomerCode = r.GetString(r.GetOrdinal("customer_code")),
                LocationId = r.IsDBNull(r.GetOrdinal("location_id")) ? null : r.GetInt32(r.GetOrdinal("location_id")),
                TradeNameId = r.IsDBNull(r.GetOrdinal("trade_name_id")) ? null : r.GetInt32(r.GetOrdinal("trade_name_id")),
                ServiceTypeId = r.GetInt32(r.GetOrdinal("service_type_id")),
                FrequencyId = r.IsDBNull(r.GetOrdinal("frequency_id")) ? null : r.GetInt32(r.GetOrdinal("frequency_id")),
                DueDate = r.GetDateTime(r.GetOrdinal("due_date")),
                LiveDate = r.IsDBNull(r.GetOrdinal("live_date")) ? null : r.GetDateTime(r.GetOrdinal("live_date")),
                ServiceValue = r.IsDBNull(r.GetOrdinal("service_value")) ? null : r.GetDecimal(r.GetOrdinal("service_value")),
                DueMonth = r.GetInt32(r.GetOrdinal("due_month")),
                AmcPercentage = r.IsDBNull(r.GetOrdinal("amc_percentage")) ? null : r.GetDecimal(r.GetOrdinal("amc_percentage")),
                AmcAmount = r.IsDBNull(r.GetOrdinal("amc_amount")) ? null : r.GetDecimal(r.GetOrdinal("amc_amount")),
                ImplementationRequired = r.GetBoolean(r.GetOrdinal("implementation_required")),
                ImplementationStatus = ParseWorkflowStatus(r.GetString(r.GetOrdinal("implementation_status"))),
                ImplementationStageId = r.IsDBNull(r.GetOrdinal("implementation_stage_id")) ? null : r.GetInt32(r.GetOrdinal("implementation_stage_id")),
                ImplementationStartedAt = r.IsDBNull(r.GetOrdinal("implementation_started_at")) ? null : r.GetDateTime(r.GetOrdinal("implementation_started_at")),
                ImplementationStartedBy = r.IsDBNull(r.GetOrdinal("implementation_started_by")) ? null : r.GetString(r.GetOrdinal("implementation_started_by")),
                ImplementationCompletedAt = r.IsDBNull(r.GetOrdinal("implementation_completed_at")) ? null : r.GetDateTime(r.GetOrdinal("implementation_completed_at")),
                ImplementationCompletedBy = r.IsDBNull(r.GetOrdinal("implementation_completed_by")) ? null : r.GetString(r.GetOrdinal("implementation_completed_by")),
                ProjectTitle = r.IsDBNull(r.GetOrdinal("project_title")) ? null : r.GetString(r.GetOrdinal("project_title")),
                ProjectManagerId = r.IsDBNull(r.GetOrdinal("project_manager_id")) ? null : r.GetInt32(r.GetOrdinal("project_manager_id")),
                BudgetAmount = r.IsDBNull(r.GetOrdinal("budget_amount")) ? null : r.GetDecimal(r.GetOrdinal("budget_amount")),
                ProgressPercentage = r.IsDBNull(r.GetOrdinal("progress_percentage")) ? null : r.GetInt32(r.GetOrdinal("progress_percentage")),
                TaxId = r.IsDBNull(r.GetOrdinal("tax_id")) ? null : r.GetInt32(r.GetOrdinal("tax_id")),
                Notes = r.IsDBNull(r.GetOrdinal("notes")) ? null : r.GetString(r.GetOrdinal("notes")),
                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                CreatedBy = r.IsDBNull(r.GetOrdinal("created_by")) ? null : r.GetInt64(r.GetOrdinal("created_by")),
                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
            };
        }

        private async Task<int?> GetCustomerIdByCodeAsync(IDb db, string customerCode)
        {
            var cmd = db.GetCommand("SELECT id FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "code", DbTypes.Types.String).Value = customerCode.Trim();
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync()) return r.GetInt32(r.GetOrdinal("id"));
            }
            return null;
        }

        private async Task<string?> GetCustomerCodeByIdAsync(IDb db, int customerId)
        {
            if (customerId <= 0) return null;
            var cmd = db.GetCommand("SELECT code FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = customerId;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync()) return r.IsDBNull(r.GetOrdinal("code")) ? null : r.GetString(r.GetOrdinal("code"));
            }
            return null;
        }

        private async Task<(string CustomerCode, int CustomerId, string? Error)> ResolveCustomerLinkAsync(
            IDb db,
            int customerId,
            string? customerCode)
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

        private async Task<(int? LocationId, string? Error)> ResolveOptionalLocationIdAsync(
            IDb db,
            string customerCode,
            int? locationId,
            string? locationCode)
        {
            if (string.IsNullOrWhiteSpace(customerCode))
                return (null, "Customer code is required to resolve location");

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
                        return (null, $"Unknown location code \"{trimmed}\" for this customer");
                    return (r.GetInt32(r.GetOrdinal("id")), null);
                }
            }

            return (locationId, null);
        }

        /// <summary>Surfaces PostgreSQL errors from nested exception chain.</summary>
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
        private static async Task<int?> ResolveStaffIdFromServiceCreatedByAsync(IDb db, long? serviceCreatedBy)
        {
            if (serviceCreatedBy is null || serviceCreatedBy <= 0 || serviceCreatedBy > int.MaxValue)
                return null;
            var uid = (int)serviceCreatedBy;
            var cmd = db.GetCommand("SELECT 1 FROM users WHERE id=@id LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = uid;
            using (DbDataReader reader = await db.Execute(cmd))
                return await reader.ReadAsync() ? uid : null;
        }

        private static ServiceResponseDto MapService(Service s) => new()
        {
            Id = s.Id,
            CustomerId = s.Customer?.Id ?? s.CustomerId,
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

        private static async Task<(int PaymentModeId, int PaymentStatusId)> GetDefaultInvoicePaymentRefsAsync(IDb db)
        {
            int modeId = 0;
            int pendingId = 0;

            var modeCmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE category='Payment Mode' AND is_active=true
ORDER BY sort_order, id
LIMIT 1;");
            using (DbDataReader r = await db.Execute(modeCmd))
            {
                if (await r.ReadAsync())
                    modeId = r.GetInt32(r.GetOrdinal("id"));
            }

            var pendingCmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE category='Payment Status' AND is_active=true AND lower(value)='pending'
ORDER BY sort_order, id
LIMIT 1;");
            using (DbDataReader r2 = await db.Execute(pendingCmd))
            {
                if (await r2.ReadAsync())
                    pendingId = r2.GetInt32(r2.GetOrdinal("id"));
            }

            if (pendingId == 0)
            {
                var fallbackCmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE category='Payment Status' AND is_active=true
ORDER BY sort_order, id
LIMIT 1;");
                using (DbDataReader r3 = await db.Execute(fallbackCmd))
                {
                    if (await r3.ReadAsync())
                        pendingId = r3.GetInt32(r3.GetOrdinal("id"));
                }
            }

            if (modeId == 0 || pendingId == 0)
                throw new InvalidOperationException("Reference data missing: ensure 'Payment Mode' and 'Payment Status' rows exist.");

            return (modeId, pendingId);
        }

        private static async Task<ReferenceEntry?> LoadReferenceEntryAsync(IDb db, int id)
        {
            if (id <= 0) return null;
            var cmd = db.GetCommand("SELECT id, category, label, value, is_active, sort_order FROM reference_entries WHERE id=@id LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (!await r.ReadAsync())
                    return null;
                return new ReferenceEntry
                {
                    Id = r.GetInt32(r.GetOrdinal("id")),
                    Category = r.IsDBNull(r.GetOrdinal("category")) ? "" : r.GetString(r.GetOrdinal("category")),
                    Label = r.IsDBNull(r.GetOrdinal("label")) ? "" : r.GetString(r.GetOrdinal("label")),
                    Value = r.IsDBNull(r.GetOrdinal("value")) ? "" : r.GetString(r.GetOrdinal("value")),
                    IsActive = !r.IsDBNull(r.GetOrdinal("is_active")) && r.GetBoolean(r.GetOrdinal("is_active")),
                    SortOrder = r.IsDBNull(r.GetOrdinal("sort_order")) ? 0 : r.GetInt32(r.GetOrdinal("sort_order"))
                };
            }
        }

        /// <summary>
        /// Creates or updates the primary invoice for this service: receivable = base amount + GST.
        /// Removes unpaid invoices when the service no longer has a positive base amount.
        /// </summary>
        private async Task SyncBillingInvoiceForServiceAsync(IDb db, Service service)
        {
            var baseAmount = service.ServiceValue ?? 0;
            if (baseAmount <= 0)
            {
                // Soft delete: mark unpaid invoices inactive when service has no positive base amount.
                var del = db.GetCommand("UPDATE invoices SET is_active=false WHERE service_id=@sid AND received<=0;");
                db.AddParameter(del, "sid", DbTypes.Types.Integer).Value = service.Id;
                await db.ExecuteNonQuery(del);
                return;
            }

            ReferenceEntry? taxEntry = null;
            if (service.TaxId.HasValue)
            {
                taxEntry = await LoadReferenceEntryAsync(db, service.TaxId.Value);
            }

            var taxPct = ResolveTaxPercent(taxEntry);
            var receivable = ComputeReceivable(baseAmount, taxPct);
            var now = DateTime.UtcNow;

            Invoice? invoice = null;
            var find = db.GetCommand(@"
SELECT id, invoice_number, customer_id, customer_code, service_id, staff_id, payment_mode_id, payment_status_id,
       receivable, received, subscription_start_at, subscription_end_at, is_active,
       created_at, created_by, modified_at, modified_by, paid_at, paid_by
FROM invoices
WHERE service_id=@sid AND invoice_number NOT LIKE 'INV-AMC-%'
ORDER BY id DESC
LIMIT 1;");
            db.AddParameter(find, "sid", DbTypes.Types.Integer).Value = service.Id;
            using (DbDataReader rInv = await db.Execute(find))
            {
                if (await rInv.ReadAsync())
                {
                    invoice = new Invoice
                    {
                        Id = rInv.GetInt32(rInv.GetOrdinal("id")),
                        InvoiceNumber = rInv.GetString(rInv.GetOrdinal("invoice_number")),
                        CustomerId = rInv.GetInt32(rInv.GetOrdinal("customer_id")),
                        CustomerCode = rInv.GetString(rInv.GetOrdinal("customer_code")),
                        ServiceId = rInv.GetInt32(rInv.GetOrdinal("service_id")),
                        StaffId = rInv.IsDBNull(rInv.GetOrdinal("staff_id")) ? null : rInv.GetInt32(rInv.GetOrdinal("staff_id")),
                        PaymentModeId = rInv.GetInt32(rInv.GetOrdinal("payment_mode_id")),
                        PaymentStatusId = rInv.GetInt32(rInv.GetOrdinal("payment_status_id")),
                        Receivable = rInv.GetDecimal(rInv.GetOrdinal("receivable")),
                        Received = rInv.GetDecimal(rInv.GetOrdinal("received")),
                        SubscriptionStartAt = rInv.GetDateTime(rInv.GetOrdinal("subscription_start_at")),
                        SubscriptionEndAt = rInv.GetDateTime(rInv.GetOrdinal("subscription_end_at")),
                        IsActive = rInv.GetBoolean(rInv.GetOrdinal("is_active")),
                        CreatedAt = rInv.GetDateTime(rInv.GetOrdinal("created_at")),
                        CreatedBy = rInv.IsDBNull(rInv.GetOrdinal("created_by")) ? null : rInv.GetInt64(rInv.GetOrdinal("created_by")),
                        ModifiedAt = rInv.GetDateTime(rInv.GetOrdinal("modified_at")),
                        ModifiedBy = rInv.IsDBNull(rInv.GetOrdinal("modified_by")) ? null : rInv.GetInt64(rInv.GetOrdinal("modified_by")),
                        PaidAt = rInv.IsDBNull(rInv.GetOrdinal("paid_at")) ? null : rInv.GetDateTime(rInv.GetOrdinal("paid_at")),
                        PaidBy = rInv.IsDBNull(rInv.GetOrdinal("paid_by")) ? null : rInv.GetString(rInv.GetOrdinal("paid_by")),
                    };
                }
            }

            // IMPORTANT: Do NOT bind subscription dates unless the service is explicitly marked live.
            // When LiveDate is not set, keep invoice dates at +/-infinity.
            // Npgsql represents +/-infinity as DateTime.MinValue/MaxValue; API normalizes those to null for UI.
            var startAt = service.LiveDate.HasValue ? service.LiveDate.Value.Date : DateTime.MinValue;
            var endAt = service.LiveDate.HasValue ? startAt.AddYears(1) : DateTime.MaxValue;
            if (service.LiveDate.HasValue && service.FrequencyId.HasValue)
            {
                var freq = await LoadReferenceEntryAsync(db, service.FrequencyId.Value);
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
                var (modeId, statusId) = await GetDefaultInvoicePaymentRefsAsync(db);
                var staffFromServiceCreator = await ResolveStaffIdFromServiceCreatedByAsync(db, service.CreatedBy);
                var invNo = $"INV-S{service.Id}-{now:yyyyMMddHHmmss}";
                var insert = db.GetCommand(@"
INSERT INTO invoices (
    invoice_number, customer_id, customer_code, service_id, staff_id,
    payment_mode_id, payment_status_id,
    receivable, received,
    subscription_start_at, subscription_end_at,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @invoice_number, @customer_id, @customer_code, @service_id, @staff_id,
    @payment_mode_id, @payment_status_id,
    @receivable, 0,
    @subscription_start_at, @subscription_end_at,
    true, @created_at, @created_by, @modified_at, @modified_by
);");
                db.AddParameter(insert, "invoice_number", DbTypes.Types.String).Value = invNo;
                db.AddParameter(insert, "customer_id", DbTypes.Types.Integer).Value = service.CustomerId;
                db.AddParameter(insert, "customer_code", DbTypes.Types.String).Value = service.CustomerCode;
                db.AddParameter(insert, "service_id", DbTypes.Types.Integer).Value = service.Id;
                db.AddParameter(insert, "staff_id", DbTypes.Types.Integer).Value = staffFromServiceCreator.HasValue ? staffFromServiceCreator.Value : DBNull.Value;
                db.AddParameter(insert, "payment_mode_id", DbTypes.Types.Integer).Value = modeId;
                db.AddParameter(insert, "payment_status_id", DbTypes.Types.Integer).Value = statusId;
                db.AddParameter(insert, "receivable", DbTypes.Types.Decimal).Value = receivable;
                db.AddParameter(insert, "subscription_start_at", DbTypes.Types.DateTime).Value = startAt;
                db.AddParameter(insert, "subscription_end_at", DbTypes.Types.DateTime).Value = endAt;
                db.AddParameter(insert, "created_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(insert, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                db.AddParameter(insert, "modified_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(insert, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                await db.ExecuteNonQuery(insert);
            }
            else
            {
                var update = db.GetCommand(@"
UPDATE invoices SET
    customer_id=@customer_id,
    customer_code=@customer_code,
    receivable=@receivable,
    subscription_start_at=@subscription_start_at,
    subscription_end_at=@subscription_end_at,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                db.AddParameter(update, "id", DbTypes.Types.Integer).Value = invoice.Id;
                db.AddParameter(update, "customer_id", DbTypes.Types.Integer).Value = service.CustomerId;
                db.AddParameter(update, "customer_code", DbTypes.Types.String).Value = service.CustomerCode;
                db.AddParameter(update, "receivable", DbTypes.Types.Decimal).Value = receivable;
                db.AddParameter(update, "subscription_start_at", DbTypes.Types.DateTime).Value = startAt;
                db.AddParameter(update, "subscription_end_at", DbTypes.Types.DateTime).Value = endAt;
                db.AddParameter(update, "modified_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(update, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                await db.ExecuteNonQuery(update);
            }
        }

        private async Task EnsureAmcInvoiceAsync(IDb db, Service service, DateTime currentStartAt, DateTime currentEndAt)
        {
            var amcAmount = service.AmcAmount;
            if (!amcAmount.HasValue && service.AmcPercentage.HasValue && (service.ServiceValue ?? 0) > 0)
            {
                amcAmount = Math.Round((service.ServiceValue!.Value * (service.AmcPercentage.Value / 100m)), 2, MidpointRounding.AwayFromZero);
            }

            if (!amcAmount.HasValue || amcAmount.Value <= 0)
                return;

            // AMC starts right AFTER the ERP/service subscription period ends.
            // Example: ERP Mar 31 2026 -> Mar 31 2027, then AMC Apr 1 2027 -> Mar 31 2028.
            var nextStart = currentEndAt.Date.AddDays(1);
            var nextEnd = nextStart.AddYears(1).AddDays(-1);

            // Resolve "AMC" service type id from reference data (category: Service Type, value: amc).
            int amcTypeId = 0;
            var amcTypeCmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE category='Service Type' AND is_active=true AND lower(value)='amc'
ORDER BY id
LIMIT 1;");
            using (DbDataReader r = await db.Execute(amcTypeCmd))
            {
                if (await r.ReadAsync())
                    amcTypeId = r.GetInt32(r.GetOrdinal("id"));
            }
            if (amcTypeId <= 0)
                return;

            // Ensure there is an AMC service row to bind this invoice.
            int? amcServiceId = null;
            var findAmcSvc = db.GetCommand(@"
SELECT id
FROM services
WHERE customer_code=@cc
  AND service_type_id=@stid
  AND live_date IS NOT NULL
  AND live_date::date = @d::date
ORDER BY id DESC
LIMIT 1;");
            db.AddParameter(findAmcSvc, "cc", DbTypes.Types.String).Value = service.CustomerCode;
            db.AddParameter(findAmcSvc, "stid", DbTypes.Types.Integer).Value = amcTypeId;
            db.AddParameter(findAmcSvc, "d", DbTypes.Types.DateTime).Value = nextStart;
            using (DbDataReader r2 = await db.Execute(findAmcSvc))
            {
                if (await r2.ReadAsync())
                    amcServiceId = r2.GetInt32(r2.GetOrdinal("id"));
            }
            if (!amcServiceId.HasValue)
            {
                var now = DateTime.UtcNow;

                // AMC should always be billed yearly (independent of the ERP service's frequency).
                int? yearlyFreqId = null;
                var yearlyFreqCmd = db.GetCommand(@"
SELECT id
FROM reference_entries
WHERE category='Frequency' AND is_active=true AND lower(value)='yearly'
ORDER BY id
LIMIT 1;");
                using (DbDataReader ry = await db.Execute(yearlyFreqCmd))
                {
                    if (await ry.ReadAsync())
                        yearlyFreqId = ry.GetInt32(ry.GetOrdinal("id"));
                }

                var insertSvc = db.GetCommand(@"
INSERT INTO services (
    customer_id, customer_code, location_id, trade_name_id, service_type_id, frequency_id,
    due_date, live_date, service_value, due_month,
    amc_percentage, amc_amount,
    implementation_required, implementation_status, implementation_stage_id,
    implementation_started_at, implementation_started_by,
    implementation_completed_at, implementation_completed_by,
    project_title, project_manager_id, budget_amount, progress_percentage,
    tax_id, notes, is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @customer_id, @customer_code, @location_id, @trade_name_id, @service_type_id, @frequency_id,
    @due_date, @live_date, @service_value, @due_month,
    NULL, NULL,
    false, 'OPEN', NULL,
    NULL, NULL,
    NULL, NULL,
    NULL, NULL, NULL, 0,
    @tax_id, @notes, true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                db.AddParameter(insertSvc, "customer_id", DbTypes.Types.Integer).Value = service.CustomerId;
                db.AddParameter(insertSvc, "customer_code", DbTypes.Types.String).Value = service.CustomerCode;
                db.AddParameter(insertSvc, "location_id", DbTypes.Types.Integer).Value = service.LocationId.HasValue ? service.LocationId.Value : DBNull.Value;
                db.AddParameter(insertSvc, "trade_name_id", DbTypes.Types.Integer).Value = service.TradeNameId.HasValue ? service.TradeNameId.Value : DBNull.Value;
                db.AddParameter(insertSvc, "service_type_id", DbTypes.Types.Integer).Value = amcTypeId;
                db.AddParameter(insertSvc, "frequency_id", DbTypes.Types.Integer).Value = yearlyFreqId.HasValue ? yearlyFreqId.Value : DBNull.Value;
                db.AddParameter(insertSvc, "due_date", DbTypes.Types.DateTime).Value = nextStart;
                db.AddParameter(insertSvc, "live_date", DbTypes.Types.DateTime).Value = nextStart;
                db.AddParameter(insertSvc, "service_value", DbTypes.Types.Decimal).Value = amcAmount.Value;
                db.AddParameter(insertSvc, "due_month", DbTypes.Types.Integer).Value = nextStart.Month;
                db.AddParameter(insertSvc, "tax_id", DbTypes.Types.Integer).Value = service.TaxId.HasValue ? service.TaxId.Value : DBNull.Value;
                db.AddParameter(insertSvc, "notes", DbTypes.Types.String).Value = $"AMC for service #{service.Id}";
                db.AddParameter(insertSvc, "created_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(insertSvc, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                db.AddParameter(insertSvc, "modified_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(insertSvc, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                using (DbDataReader rr = await db.Execute(insertSvc))
                {
                    if (await rr.ReadAsync())
                        amcServiceId = rr.GetInt32(rr.GetOrdinal("id"));
                }
            }

            if (!amcServiceId.HasValue || amcServiceId.Value <= 0)
                return;

            // Avoid duplicate AMC invoices for this AMC service + period.
            bool exists = false;
            var existsCmd = db.GetCommand(@"
SELECT EXISTS (
  SELECT 1 FROM invoices
  WHERE service_id=@sid
    AND invoice_number LIKE 'INV-AMC-%'
    AND subscription_start_at=@sa
    AND subscription_end_at=@ea
  LIMIT 1
) AS has;");
            db.AddParameter(existsCmd, "sid", DbTypes.Types.Integer).Value = amcServiceId.Value;
            db.AddParameter(existsCmd, "sa", DbTypes.Types.DateTime).Value = nextStart;
            db.AddParameter(existsCmd, "ea", DbTypes.Types.DateTime).Value = nextEnd;
            using (DbDataReader r3 = await db.Execute(existsCmd))
            {
                if (await r3.ReadAsync())
                    exists = r3.GetBoolean(r3.GetOrdinal("has"));
            }
            if (exists) return;

            ReferenceEntry? taxEntry = null;
            if (service.TaxId.HasValue)
            {
                taxEntry = await LoadReferenceEntryAsync(db, service.TaxId.Value);
            }
            var taxPct = ResolveTaxPercent(taxEntry);
            var receivable = ComputeReceivable(amcAmount.Value, taxPct);

            var now2 = DateTime.UtcNow;
            var (modeId, statusId) = await GetDefaultInvoicePaymentRefsAsync(db);
            var staffFromServiceCreator = await ResolveStaffIdFromServiceCreatedByAsync(db, service.CreatedBy);
            var insertInv = db.GetCommand(@"
INSERT INTO invoices (
    invoice_number, customer_id, customer_code, service_id, staff_id,
    payment_mode_id, payment_status_id,
    receivable, received,
    subscription_start_at, subscription_end_at,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @invoice_number, @customer_id, @customer_code, @service_id, @staff_id,
    @payment_mode_id, @payment_status_id,
    @receivable, 0,
    @subscription_start_at, @subscription_end_at,
    true, @created_at, @created_by, @modified_at, @modified_by
);");
            db.AddParameter(insertInv, "invoice_number", DbTypes.Types.String).Value = $"INV-AMC-S{amcServiceId.Value}-{now2:yyyyMMddHHmmss}";
            db.AddParameter(insertInv, "customer_id", DbTypes.Types.Integer).Value = service.CustomerId;
            db.AddParameter(insertInv, "customer_code", DbTypes.Types.String).Value = service.CustomerCode;
            db.AddParameter(insertInv, "service_id", DbTypes.Types.Integer).Value = amcServiceId.Value;
            db.AddParameter(insertInv, "staff_id", DbTypes.Types.Integer).Value = staffFromServiceCreator.HasValue ? staffFromServiceCreator.Value : DBNull.Value;
            db.AddParameter(insertInv, "payment_mode_id", DbTypes.Types.Integer).Value = modeId;
            db.AddParameter(insertInv, "payment_status_id", DbTypes.Types.Integer).Value = statusId;
            db.AddParameter(insertInv, "receivable", DbTypes.Types.Decimal).Value = receivable;
            db.AddParameter(insertInv, "subscription_start_at", DbTypes.Types.DateTime).Value = nextStart;
            db.AddParameter(insertInv, "subscription_end_at", DbTypes.Types.DateTime).Value = nextEnd;
            db.AddParameter(insertInv, "created_at", DbTypes.Types.DateTime).Value = now2;
            db.AddParameter(insertInv, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
            db.AddParameter(insertInv, "modified_at", DbTypes.Types.DateTime).Value = now2;
            db.AddParameter(insertInv, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
            await db.ExecuteNonQuery(insertInv);
        }

        public async Task<ApiResponse<ServiceResponseDto>> GoLive(int id, GoLiveServiceDto dto)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        Service? service = null;
                        var cmd = db.GetCommand(@"
SELECT s.*
FROM services s
WHERE s.id=@id
LIMIT 1;");
                        db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                        using (DbDataReader r = await db.Execute(cmd))
                        {
                            if (await r.ReadAsync())
                            {
                                service = ReadService(r);
                                service.Customer = new Customer { Id = service.CustomerId };
                            }
                        }
                        if (service == null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<ServiceResponseDto> { Success = false, Message = "Service not found" };
                        }

                        var now = DateTime.UtcNow;
                        var auditUser = dto.ModifiedByUserId is { } uid && uid > 0 ? uid : AuditUserIds.System;
                        service.LiveDate = dto.LiveDate;
                        service.ModifiedAt = now;
                        service.ModifiedBy = auditUser;

                        // On "Go Live", if ERP/service is set to Monthly, convert it to One-Time
                        // so the primary invoice spans 1 year from LiveDate (ERP), not 1 month.
                        if (service.FrequencyId.HasValue)
                        {
                            var freq = await LoadReferenceEntryAsync(db, service.FrequencyId.Value);
                            if (freq != null && !string.IsNullOrEmpty(freq.Label) &&
                                freq.Label.ToLowerInvariant().Contains("month"))
                            {
                                service.FrequencyId = null;
                            }
                        }

                        var upd = db.GetCommand(@"
UPDATE services SET
    live_date=@live_date,
    frequency_id=@frequency_id,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                        db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = service.Id;
                        db.AddParameter(upd, "live_date", DbTypes.Types.DateTime).Value = service.LiveDate.HasValue ? service.LiveDate.Value : DBNull.Value;
                        db.AddParameter(upd, "frequency_id", DbTypes.Types.Integer).Value = service.FrequencyId.HasValue ? service.FrequencyId.Value : DBNull.Value;
                        db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = service.ModifiedAt;
                        db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = auditUser;
                        await db.ExecuteNonQuery(upd);

                        await SyncBillingInvoiceForServiceAsync(db, service);
                        if (service.LiveDate.HasValue)
                        {
                            var currentStart = service.LiveDate.Value.Date;
                            var currentEnd = currentStart.AddYears(1);
                            await EnsureAmcInvoiceAsync(db, service, currentStart, currentEnd);
                        }

                        await db.CommitTransaction();
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }

                // Reload DTO with latest values + codes
                var refreshed = await GetServiceById(id);
                if (!refreshed.Success || refreshed.Data == null)
                    return refreshed;
                return new ApiResponse<ServiceResponseDto> { Success = true, Message = "Service marked live", Data = refreshed.Data };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ServiceResponseDto> { Success = false, Message = $"Error marking service live: {FormatPersistenceError(ex)}" };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<ServiceResponseDto>>> GetAllServices(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var offset = Math.Max(0, (pageNumber - 1) * pageSize);
                int total = 0;
                var services = new List<Service>();
                var locCodeByServiceId = new Dictionary<int, string?>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var countCmd = db.GetCommand("SELECT COUNT(*)::int AS total FROM services WHERE is_active=true;");
                    using (DbDataReader r = await db.Execute(countCmd))
                    {
                        if (await r.ReadAsync())
                            total = r.GetInt32(r.GetOrdinal("total"));
                    }

                    var listCmd = db.GetCommand(@"
SELECT s.*, l.code AS location_code
FROM services s
LEFT JOIN locations l ON l.id = s.location_id
WHERE s.is_active = true
ORDER BY s.id DESC
LIMIT @limit OFFSET @offset;");
                    db.AddParameter(listCmd, "limit", DbTypes.Types.Integer).Value = pageSize;
                    db.AddParameter(listCmd, "offset", DbTypes.Types.Integer).Value = offset;
                    using (DbDataReader r2 = await db.Execute(listCmd))
                    {
                        while (await r2.ReadAsync())
                        {
                            var s = ReadService(r2);
                            s.Customer = new Customer { Id = s.CustomerId };
                            services.Add(s);
                            locCodeByServiceId[s.Id] = r2.IsDBNull(r2.GetOrdinal("location_code")) ? null : r2.GetString(r2.GetOrdinal("location_code"));
                        }
                    }

                    var items = services.Select(MapService).ToList();
                    foreach (var dto in items)
                    {
                        dto.CustomerCode = services.FirstOrDefault(x => x.Id == dto.Id)?.CustomerCode;
                        if (locCodeByServiceId.TryGetValue(dto.Id, out var lc))
                            dto.LocationCode = lc;
                    }
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

        public async Task<ApiResponse<List<ServiceResponseDto>>> GetAllServicesList(ServiceQueryDto q)
        {
            try
            {
                var list = new List<Service>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var where = new List<string>();
                    if (!(q.IncludeInactive ?? false)) where.Add("s.is_active=true");
                    if (q.CustomerId is > 0) where.Add("s.customer_id=@customer_id");
                    if (q.ServiceTypeId is > 0) where.Add("s.service_type_id=@service_type_id");
                    if (q.FrequencyId is > 0) where.Add("s.frequency_id=@frequency_id");
                    if (q.ImplementationStatusId is > 0) where.Add("s.implementation_status_id=@implementation_status_id");
                    if (q.From != null) where.Add("s.created_at >= @from");
                    if (q.To != null) where.Add("s.created_at < @to");
                    if (!string.IsNullOrWhiteSpace(q.Search))
                    {
                        // No customer join here; keep it light.
                        where.Add("(s.notes ILIKE @search)");
                    }

                    var sql = @"
SELECT s.*, l.code AS location_code
FROM services s
LEFT JOIN locations l ON l.id = s.location_id
";
                    if (where.Count > 0) sql += "WHERE " + string.Join(" AND ", where) + "\n";
                    sql += "ORDER BY s.id DESC;";
                    var cmd = db.GetCommand(sql);
                    if (q.CustomerId is > 0) db.AddParameter(cmd, "customer_id", DbTypes.Types.Integer).Value = q.CustomerId.Value;
                    if (q.ServiceTypeId is > 0) db.AddParameter(cmd, "service_type_id", DbTypes.Types.Integer).Value = q.ServiceTypeId.Value;
                    if (q.FrequencyId is > 0) db.AddParameter(cmd, "frequency_id", DbTypes.Types.Integer).Value = q.FrequencyId.Value;
                    if (q.ImplementationStatusId is > 0) db.AddParameter(cmd, "implementation_status_id", DbTypes.Types.Integer).Value = q.ImplementationStatusId.Value;
                    if (q.From != null) db.AddParameter(cmd, "from", DbTypes.Types.DateTime).Value = q.From.Value.Date;
                    if (q.To != null) db.AddParameter(cmd, "to", DbTypes.Types.DateTime).Value = q.To.Value.Date.AddDays(1);
                    if (!string.IsNullOrWhiteSpace(q.Search)) db.AddParameter(cmd, "search", DbTypes.Types.String).Value = "%" + q.Search!.Trim() + "%";
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            var s = ReadService(r);
                            s.Customer = new Customer { Id = s.CustomerId };
                            list.Add(s);
                        }
                    }

                    var dtos = list.Select(MapService).ToList();
                    foreach (var d in dtos)
                    {
                        var s = list.FirstOrDefault(x => x.Id == d.Id);
                        d.CustomerCode = s?.CustomerCode;
                        if (d.LocationId is { } lid)
                        {
                            var lcCmd = db.GetCommand("SELECT code FROM locations WHERE id=@id LIMIT 1;");
                            db.AddParameter(lcCmd, "id", DbTypes.Types.Integer).Value = lid;
                            using (DbDataReader rr = await db.Execute(lcCmd))
                            {
                                if (await rr.ReadAsync())
                                    d.LocationCode = rr.IsDBNull(rr.GetOrdinal("code")) ? null : rr.GetString(rr.GetOrdinal("code"));
                            }
                        }
                    }
                    return new ApiResponse<List<ServiceResponseDto>> { Success = true, Data = dtos };
                }
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
                Service? service = null;
                string? locationCode = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT s.*, l.code AS location_code
FROM services s
LEFT JOIN locations l ON l.id = s.location_id
WHERE s.id=@id
LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                        {
                            service = ReadService(r);
                            service.Customer = new Customer { Id = service.CustomerId };
                            locationCode = r.IsDBNull(r.GetOrdinal("location_code")) ? null : r.GetString(r.GetOrdinal("location_code"));
                        }
                    }
                }
                if (service == null)
                    return new ApiResponse<ServiceResponseDto> { Success = false, Message = "Service not found" };
                var one = MapService(service);
                one.CustomerCode = service.CustomerCode;
                one.LocationCode = locationCode;
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
                string? cc = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    cc = await GetCustomerCodeByIdAsync(db, customerId);
                }
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<ServiceResponseDto>> { Success = true, Data = new List<ServiceResponseDto>() };

                var list = new List<Service>();
                var dtos = new List<ServiceResponseDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT s.*, l.code AS location_code
FROM services s
LEFT JOIN locations l ON l.id = s.location_id
WHERE s.customer_id=@cid
ORDER BY s.id DESC;");
                    db.AddParameter(cmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            var s = ReadService(r);
                            s.Customer = new Customer { Id = s.CustomerId };
                            list.Add(s);
                            var d = MapService(s);
                            d.CustomerCode = s.CustomerCode;
                            d.LocationCode = r.IsDBNull(r.GetOrdinal("location_code")) ? null : r.GetString(r.GetOrdinal("location_code"));
                            dtos.Add(d);
                        }
                    }
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var (custCode, cid, err) = await ResolveCustomerLinkAsync(db, 0, customerCode);
                    if (err != null)
                        return new ApiResponse<List<ServiceResponseDto>> { Success = false, Message = err };
                    return await GetServicesByCustomer(cid);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ServiceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ServiceResponseDto>> CreateService(CreateServiceDto dto)
        {
            try
            {
                int newId = 0;
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
                            return new ApiResponse<ServiceResponseDto> { Success = false, Message = cErr };
                        }

                        var (locId, lErr) = await ResolveOptionalLocationIdAsync(db, custCode, dto.LocationId, dto.LocationCode);
                        if (lErr != null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<ServiceResponseDto> { Success = false, Message = lErr };
                        }

                        var now = DateTime.UtcNow;
                        var dueMonth = dto.DueMonth is > 0 ? dto.DueMonth.Value : dto.DueDate.Month;
                        var impl = dto.ImplementationStatusId is { } sid ? ApiCodeToWorkflow(sid) : ImplementationWorkflowStatus.OPEN;

                        var insert = db.GetCommand(@"
INSERT INTO services (
    customer_id, customer_code, location_id, trade_name_id, service_type_id, frequency_id,
    due_date, live_date, service_value, due_month,
    amc_percentage, amc_amount,
    implementation_required, implementation_status, implementation_stage_id,
    implementation_started_at, implementation_started_by,
    implementation_completed_at, implementation_completed_by,
    project_title, project_manager_id, budget_amount, progress_percentage,
    tax_id, notes, is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @customer_id, @customer_code, @location_id, @trade_name_id, @service_type_id, @frequency_id,
    @due_date, @live_date, @service_value, @due_month,
    @amc_percentage, @amc_amount,
    @implementation_required, @implementation_status::implementation_status_enum, NULL,
    NULL, NULL,
    NULL, NULL,
    @project_title, @project_manager_id, @budget_amount, 0,
    @tax_id, @notes, true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                        db.AddParameter(insert, "customer_id", DbTypes.Types.Integer).Value = custId;
                        db.AddParameter(insert, "customer_code", DbTypes.Types.String).Value = custCode;
                        db.AddParameter(insert, "location_id", DbTypes.Types.Integer).Value = locId.HasValue ? locId.Value : DBNull.Value;
                        db.AddParameter(insert, "trade_name_id", DbTypes.Types.Integer).Value = dto.TradeNameId.HasValue ? dto.TradeNameId.Value : DBNull.Value;
                        db.AddParameter(insert, "service_type_id", DbTypes.Types.Integer).Value = dto.ServiceTypeId;
                        db.AddParameter(insert, "frequency_id", DbTypes.Types.Integer).Value = dto.FrequencyId.HasValue ? dto.FrequencyId.Value : DBNull.Value;
                        db.AddParameter(insert, "due_date", DbTypes.Types.DateTime).Value = dto.DueDate;
                        db.AddParameter(insert, "live_date", DbTypes.Types.DateTime).Value = dto.LiveDate.HasValue ? dto.LiveDate.Value : DBNull.Value;
                        db.AddParameter(insert, "service_value", DbTypes.Types.Decimal).Value = dto.ServiceValue.HasValue ? dto.ServiceValue.Value : DBNull.Value;
                        db.AddParameter(insert, "due_month", DbTypes.Types.Integer).Value = dueMonth;
                        db.AddParameter(insert, "amc_percentage", DbTypes.Types.Decimal).Value = dto.AmcPercentage.HasValue ? dto.AmcPercentage.Value : DBNull.Value;
                        db.AddParameter(insert, "amc_amount", DbTypes.Types.Decimal).Value = dto.AmcAmount.HasValue ? dto.AmcAmount.Value : DBNull.Value;
                        db.AddParameter(insert, "implementation_required", DbTypes.Types.Boolean).Value = dto.ImplementationRequired;
                        db.AddParameter(insert, "implementation_status", DbTypes.Types.String).Value = WorkflowToDb(impl);
                        db.AddParameter(insert, "project_title", DbTypes.Types.String).Value = dto.ProjectTitle ?? (object)DBNull.Value;
                        db.AddParameter(insert, "project_manager_id", DbTypes.Types.Integer).Value = dto.ProjectManagerId.HasValue ? dto.ProjectManagerId.Value : DBNull.Value;
                        db.AddParameter(insert, "budget_amount", DbTypes.Types.Decimal).Value = dto.BudgetAmount.HasValue ? dto.BudgetAmount.Value : DBNull.Value;
                        db.AddParameter(insert, "tax_id", DbTypes.Types.Integer).Value = dto.TaxId.HasValue ? dto.TaxId.Value : DBNull.Value;
                        db.AddParameter(insert, "notes", DbTypes.Types.String).Value = dto.Notes ?? (object)DBNull.Value;
                        db.AddParameter(insert, "created_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(insert, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                        db.AddParameter(insert, "modified_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(insert, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                        using (DbDataReader rr = await db.Execute(insert))
                        {
                            if (await rr.ReadAsync())
                                newId = rr.GetInt32(rr.GetOrdinal("id"));
                        }

                        // Invoice + (optional) AMC in same transaction
                        var svcLoad = db.GetCommand(@"
SELECT s.*
FROM services s
WHERE s.id=@id
LIMIT 1;");
                        db.AddParameter(svcLoad, "id", DbTypes.Types.Integer).Value = newId;
                        Service createdSvc;
                        using (DbDataReader rSvc = await db.Execute(svcLoad))
                        {
                            await rSvc.ReadAsync();
                            createdSvc = ReadService(rSvc);
                            createdSvc.Customer = new Customer { Id = createdSvc.CustomerId };
                        }
                        await SyncBillingInvoiceForServiceAsync(db, createdSvc);
                        if (createdSvc.LiveDate.HasValue)
                        {
                            var currentStart = createdSvc.LiveDate.Value.Date;
                            var currentEnd = currentStart.AddYears(1);
                            await EnsureAmcInvoiceAsync(db, createdSvc, currentStart, currentEnd);
                        }

                        await db.CommitTransaction();
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }

                var created = await GetServiceById(newId);
                if (!created.Success) return created;
                created.Message = "Service created successfully";
                return created;
            }
            catch (Exception ex)
            {
                return new ApiResponse<ServiceResponseDto> { Success = false, Message = $"Error creating service: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<ServiceResponseDto>> UpdateService(int id, UpdateServiceDto dto)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        Service? service = null;
                        var load = db.GetCommand(@"
SELECT s.*
FROM services s
WHERE s.id=@id
LIMIT 1;");
                        db.AddParameter(load, "id", DbTypes.Types.Integer).Value = id;
                        using (DbDataReader r = await db.Execute(load))
                        {
                            if (await r.ReadAsync())
                            {
                                service = ReadService(r);
                                service.Customer = new Customer { Id = service.CustomerId };
                            }
                        }
                        if (service == null)
                        {
                            await db.RollbackTransaction();
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
                            ApplyImplementationWorkflowTransition(service, prevStatus, nextStatus, DateTime.UtcNow, dto.ModifiedByUserId ?? 0);
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
                                ApplyImplementationWorkflowTransition(service, prev, ImplementationWorkflowStatus.OPEN, now, uid);
                        }

                        if (dto.IsActive.HasValue) service.IsActive = dto.IsActive.Value;
                        if (dto.Notes != null) service.Notes = dto.Notes;

                        if (dto.UpdateBillingLinks == true)
                        {
                            if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                            {
                                var (lid, lErr) = await ResolveOptionalLocationIdAsync(db, service.CustomerCode, null, dto.LocationCode);
                                if (lErr != null)
                                {
                                    await db.RollbackTransaction();
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
                            service.LiveDate = dto.LiveDate;
                        }

                        if (dto.ProjectManagerId.HasValue)
                            service.ProjectManagerId = dto.ProjectManagerId.Value <= 0 ? null : dto.ProjectManagerId.Value;
                        if (dto.ProgressPercentage.HasValue)
                            service.ProgressPercentage = Math.Clamp(dto.ProgressPercentage.Value, 0, 100);

                        service.ModifiedAt = DateTime.UtcNow;
                        service.ModifiedBy = dto.ModifiedByUserId > 0 ? dto.ModifiedByUserId : AuditUserIds.System;

                        var upd = db.GetCommand(@"
UPDATE services SET
    location_id=@location_id,
    trade_name_id=@trade_name_id,
    service_type_id=@service_type_id,
    frequency_id=@frequency_id,
    due_date=@due_date,
    live_date=@live_date,
    service_value=@service_value,
    due_month=@due_month,
    amc_percentage=@amc_percentage,
    amc_amount=@amc_amount,
    implementation_required=@implementation_required,
    implementation_status=@implementation_status::implementation_status_enum,
    implementation_stage_id=@implementation_stage_id,
    implementation_started_at=@implementation_started_at,
    implementation_started_by=@implementation_started_by,
    implementation_completed_at=@implementation_completed_at,
    implementation_completed_by=@implementation_completed_by,
    project_title=@project_title,
    project_manager_id=@project_manager_id,
    budget_amount=@budget_amount,
    progress_percentage=@progress_percentage,
    tax_id=@tax_id,
    notes=@notes,
    is_active=@is_active,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                        db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = service.Id;
                        db.AddParameter(upd, "location_id", DbTypes.Types.Integer).Value = service.LocationId.HasValue ? service.LocationId.Value : DBNull.Value;
                        db.AddParameter(upd, "trade_name_id", DbTypes.Types.Integer).Value = service.TradeNameId.HasValue ? service.TradeNameId.Value : DBNull.Value;
                        db.AddParameter(upd, "service_type_id", DbTypes.Types.Integer).Value = service.ServiceTypeId;
                        db.AddParameter(upd, "frequency_id", DbTypes.Types.Integer).Value = service.FrequencyId.HasValue ? service.FrequencyId.Value : DBNull.Value;
                        db.AddParameter(upd, "due_date", DbTypes.Types.DateTime).Value = service.DueDate;
                        db.AddParameter(upd, "live_date", DbTypes.Types.DateTime).Value = service.LiveDate.HasValue ? service.LiveDate.Value : DBNull.Value;
                        db.AddParameter(upd, "service_value", DbTypes.Types.Decimal).Value = service.ServiceValue.HasValue ? service.ServiceValue.Value : DBNull.Value;
                        db.AddParameter(upd, "due_month", DbTypes.Types.Integer).Value = service.DueMonth;
                        db.AddParameter(upd, "amc_percentage", DbTypes.Types.Decimal).Value = service.AmcPercentage.HasValue ? service.AmcPercentage.Value : DBNull.Value;
                        db.AddParameter(upd, "amc_amount", DbTypes.Types.Decimal).Value = service.AmcAmount.HasValue ? service.AmcAmount.Value : DBNull.Value;
                        db.AddParameter(upd, "implementation_required", DbTypes.Types.Boolean).Value = service.ImplementationRequired;
                        db.AddParameter(upd, "implementation_status", DbTypes.Types.String).Value = WorkflowToDb(service.ImplementationStatus);
                        db.AddParameter(upd, "implementation_stage_id", DbTypes.Types.Integer).Value = service.ImplementationStageId.HasValue ? service.ImplementationStageId.Value : DBNull.Value;
                        db.AddParameter(upd, "implementation_started_at", DbTypes.Types.DateTime).Value = service.ImplementationStartedAt.HasValue ? service.ImplementationStartedAt.Value : DBNull.Value;
                        db.AddParameter(upd, "implementation_started_by", DbTypes.Types.String).Value = service.ImplementationStartedBy ?? (object)DBNull.Value;
                        db.AddParameter(upd, "implementation_completed_at", DbTypes.Types.DateTime).Value = service.ImplementationCompletedAt.HasValue ? service.ImplementationCompletedAt.Value : DBNull.Value;
                        db.AddParameter(upd, "implementation_completed_by", DbTypes.Types.String).Value = service.ImplementationCompletedBy ?? (object)DBNull.Value;
                        db.AddParameter(upd, "project_title", DbTypes.Types.String).Value = service.ProjectTitle ?? (object)DBNull.Value;
                        db.AddParameter(upd, "project_manager_id", DbTypes.Types.Integer).Value = service.ProjectManagerId.HasValue ? service.ProjectManagerId.Value : DBNull.Value;
                        db.AddParameter(upd, "budget_amount", DbTypes.Types.Decimal).Value = service.BudgetAmount.HasValue ? service.BudgetAmount.Value : DBNull.Value;
                        db.AddParameter(upd, "progress_percentage", DbTypes.Types.Integer).Value = service.ProgressPercentage.HasValue ? service.ProgressPercentage.Value : DBNull.Value;
                        db.AddParameter(upd, "tax_id", DbTypes.Types.Integer).Value = service.TaxId.HasValue ? service.TaxId.Value : DBNull.Value;
                        db.AddParameter(upd, "notes", DbTypes.Types.String).Value = service.Notes ?? (object)DBNull.Value;
                        db.AddParameter(upd, "is_active", DbTypes.Types.Boolean).Value = service.IsActive;
                        db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = service.ModifiedAt;
                        db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = service.ModifiedBy.HasValue ? service.ModifiedBy.Value : DBNull.Value;
                        await db.ExecuteNonQuery(upd);

                        if (dto.UpdateBillingLinks == true)
                        {
                            await SyncBillingInvoiceForServiceAsync(db, service);
                            if (service.LiveDate.HasValue)
                            {
                                var currentStart = service.LiveDate.Value.Date;
                                var currentEnd = currentStart.AddYears(1);
                                await EnsureAmcInvoiceAsync(db, service, currentStart, currentEnd);
                            }
                        }

                        await db.CommitTransaction();
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }

                var refreshed = await GetServiceById(id);
                if (!refreshed.Success) return refreshed;
                refreshed.Message = "Service updated successfully";
                return refreshed;
            }
            catch (Exception ex)
            {
                return new ApiResponse<ServiceResponseDto> { Success = false, Message = $"Error updating service: {ex.Message}" };
            }
        }

        public async Task<ApiResponse<bool>> DeleteService(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
UPDATE services
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
                            return new ApiResponse<bool> { Success = false, Message = "Service not found" };
                    }
                    return new ApiResponse<bool> { Success = true, Message = "Service deleted successfully", Data = true };
                }
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
                var rows = new List<ImplementationTimeline>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT id, service_id, type, status, notes, file_id, file_name, user_id, is_active,
       created_at, created_by, modified_at, modified_by
FROM implementation_timelines
WHERE service_id=@sid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "sid", DbTypes.Types.Integer).Value = serviceId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            rows.Add(new ImplementationTimeline
                            {
                                Id = r.GetInt32(r.GetOrdinal("id")),
                                ServiceId = r.GetInt32(r.GetOrdinal("service_id")),
                                Type = r.GetInt32(r.GetOrdinal("type")),
                                WorkflowStatus = ParseWorkflowStatus(r.GetString(r.GetOrdinal("status"))),
                                Notes = r.GetString(r.GetOrdinal("notes")),
                                FileId = r.IsDBNull(r.GetOrdinal("file_id")) ? null : r.GetInt32(r.GetOrdinal("file_id")),
                                FileName = r.IsDBNull(r.GetOrdinal("file_name")) ? null : r.GetString(r.GetOrdinal("file_name")),
                                UserId = r.GetInt32(r.GetOrdinal("user_id")),
                                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                                CreatedBy = r.GetInt64(r.GetOrdinal("created_by")),
                                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by"))
                            });
                        }
                    }
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        Service? svc = null;
                        var svcCmd = db.GetCommand("SELECT * FROM services WHERE id=@id AND is_active=true LIMIT 1;");
                        db.AddParameter(svcCmd, "id", DbTypes.Types.Integer).Value = serviceId;
                        using (DbDataReader rSvc = await db.Execute(svcCmd))
                        {
                            if (await rSvc.ReadAsync())
                                svc = ReadService(rSvc);
                        }
                        if (svc == null)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<ImplementationTimelineEntryDto> { Success = false, Message = "Service not found" };
                        }

                        // Session may reference a deleted user, or clients may send userId 0 — fall back to system / first user.
                        int? userId = null;
                        if (dto.UserId > 0)
                        {
                            var u = db.GetCommand("SELECT id FROM users WHERE id=@id LIMIT 1;");
                            db.AddParameter(u, "id", DbTypes.Types.Integer).Value = dto.UserId;
                            using (DbDataReader ru = await db.Execute(u))
                            {
                                if (await ru.ReadAsync())
                                    userId = ru.GetInt32(ru.GetOrdinal("id"));
                            }
                        }
                        if (!userId.HasValue)
                        {
                            var u2 = db.GetCommand("SELECT id FROM users WHERE id=@id LIMIT 1;");
                            db.AddParameter(u2, "id", DbTypes.Types.Integer).Value = (int)AuditUserIds.System;
                            using (DbDataReader ru2 = await db.Execute(u2))
                            {
                                if (await ru2.ReadAsync())
                                    userId = ru2.GetInt32(ru2.GetOrdinal("id"));
                            }
                        }
                        if (!userId.HasValue)
                        {
                            var u3 = db.GetCommand("SELECT id FROM users ORDER BY id LIMIT 1;");
                            using (DbDataReader ru3 = await db.Execute(u3))
                            {
                                if (await ru3.ReadAsync())
                                    userId = ru3.GetInt32(ru3.GetOrdinal("id"));
                            }
                        }
                        if (!userId.HasValue)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<ImplementationTimelineEntryDto>
                            {
                                Success = false,
                                Message = "No matching user for this request; add at least one user to the database."
                            };
                        }

                        var workflow = ApiCodeToWorkflow(dto.StatusId);
                        var now = DateTime.UtcNow;
                        var actorId = (long)userId.Value;

                        var prevSvcStatus = svc.ImplementationStatus;
                        svc.ImplementationStatus = workflow;
                        ApplyImplementationWorkflowTransition(svc, prevSvcStatus, workflow, now, actorId);
                        svc.ModifiedAt = now;
                        svc.ModifiedBy = actorId;

                        var updSvc = db.GetCommand(@"
UPDATE services SET
    implementation_status=@st,
    implementation_started_at=@isa,
    implementation_started_by=@isb,
    implementation_completed_at=@ica,
    implementation_completed_by=@icb,
    modified_at=@ma,
    modified_by=@mb
WHERE id=@id;");
                        db.AddParameter(updSvc, "id", DbTypes.Types.Integer).Value = serviceId;
                        db.AddParameter(updSvc, "st", DbTypes.Types.String).Value = WorkflowToDb(svc.ImplementationStatus);
                        db.AddParameter(updSvc, "isa", DbTypes.Types.DateTime).Value = svc.ImplementationStartedAt.HasValue ? svc.ImplementationStartedAt.Value : DBNull.Value;
                        db.AddParameter(updSvc, "isb", DbTypes.Types.String).Value = svc.ImplementationStartedBy ?? (object)DBNull.Value;
                        db.AddParameter(updSvc, "ica", DbTypes.Types.DateTime).Value = svc.ImplementationCompletedAt.HasValue ? svc.ImplementationCompletedAt.Value : DBNull.Value;
                        db.AddParameter(updSvc, "icb", DbTypes.Types.String).Value = svc.ImplementationCompletedBy ?? (object)DBNull.Value;
                        db.AddParameter(updSvc, "ma", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(updSvc, "mb", DbTypes.Types.Long).Value = actorId;
                        await db.ExecuteNonQuery(updSvc);

                        int newId = 0;
                        var ins = db.GetCommand(@"
INSERT INTO implementation_timelines (
    service_id, type, status, notes, file_id, file_name, user_id,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @sid, @type, @status, @notes, @file_id, @file_name, @user_id,
    true, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                        db.AddParameter(ins, "sid", DbTypes.Types.Integer).Value = serviceId;
                        db.AddParameter(ins, "type", DbTypes.Types.Integer).Value = dto.Type;
                        db.AddParameter(ins, "status", DbTypes.Types.String).Value = WorkflowToDb(workflow);
                        db.AddParameter(ins, "notes", DbTypes.Types.String).Value = dto.Notes ?? string.Empty;
                        db.AddParameter(ins, "file_id", DbTypes.Types.Integer).Value = dto.FileId.HasValue ? dto.FileId.Value : DBNull.Value;
                        db.AddParameter(ins, "file_name", DbTypes.Types.String).Value = dto.FileName ?? (object)DBNull.Value;
                        db.AddParameter(ins, "user_id", DbTypes.Types.Integer).Value = userId.Value;
                        db.AddParameter(ins, "created_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(ins, "created_by", DbTypes.Types.Long).Value = actorId;
                        db.AddParameter(ins, "modified_at", DbTypes.Types.DateTime).Value = now;
                        db.AddParameter(ins, "modified_by", DbTypes.Types.Long).Value = actorId;
                        using (DbDataReader rr = await db.Execute(ins))
                        {
                            if (await rr.ReadAsync())
                                newId = rr.GetInt32(rr.GetOrdinal("id"));
                        }

                        await db.CommitTransaction();

                        var e = new ImplementationTimeline
                        {
                            Id = newId,
                            ServiceId = serviceId,
                            Type = dto.Type,
                            WorkflowStatus = workflow,
                            Notes = dto.Notes ?? string.Empty,
                            FileId = dto.FileId,
                            FileName = dto.FileName,
                            UserId = userId.Value,
                            IsActive = true,
                            CreatedAt = now,
                            CreatedBy = actorId,
                            ModifiedAt = now,
                            ModifiedBy = actorId
                        };
                        return new ApiResponse<ImplementationTimelineEntryDto> { Success = true, Data = MapImplementationTimeline(e) };
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
                var rows = new List<ImplementationAssignment>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand("SELECT id, service_id, user_ids FROM implementation_assignments WHERE is_active=true ORDER BY id DESC;");
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            rows.Add(new ImplementationAssignment
                            {
                                Id = r.GetInt32(r.GetOrdinal("id")),
                                ServiceId = r.GetInt32(r.GetOrdinal("service_id")),
                                UserIds = SplitCsvInts(r.IsDBNull(r.GetOrdinal("user_ids")) ? null : r.GetString(r.GetOrdinal("user_ids")))
                            });
                        }
                    }
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        var svc = db.GetCommand("SELECT 1 FROM services WHERE id=@id AND is_active=true LIMIT 1;");
                        db.AddParameter(svc, "id", DbTypes.Types.Integer).Value = serviceId;
                        bool hasSvc = false;
                        using (DbDataReader rsvc = await db.Execute(svc))
                        {
                            hasSvc = await rsvc.ReadAsync();
                        }
                        if (!hasSvc)
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<ImplementationAssignmentDto> { Success = false, Message = "Service not found" };
                        }

                        var userIds = dto.UserIds ?? new List<int>();
                        var csv = JoinCsvInts(userIds);

                        var ids = new List<int>();
                        var sel = db.GetCommand("SELECT id FROM implementation_assignments WHERE service_id=@sid AND is_active=true ORDER BY id DESC;");
                        db.AddParameter(sel, "sid", DbTypes.Types.Integer).Value = serviceId;
                        using (DbDataReader r = await db.Execute(sel))
                        {
                            while (await r.ReadAsync())
                                ids.Add(r.GetInt32(r.GetOrdinal("id")));
                        }

                        int entityId = 0;
                        if (ids.Count == 0)
                        {
                            var ins = db.GetCommand(@"
INSERT INTO implementation_assignments (service_id, user_ids)
VALUES (@sid, @user_ids)
RETURNING id;");
                            db.AddParameter(ins, "sid", DbTypes.Types.Integer).Value = serviceId;
                            db.AddParameter(ins, "user_ids", DbTypes.Types.String).Value = csv;
                            using (DbDataReader rr = await db.Execute(ins))
                            {
                                if (await rr.ReadAsync())
                                    entityId = rr.GetInt32(rr.GetOrdinal("id"));
                            }
                        }
                        else
                        {
                            entityId = ids[0];
                            var upd = db.GetCommand("UPDATE implementation_assignments SET user_ids=@user_ids WHERE id=@id;");
                            db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = entityId;
                            db.AddParameter(upd, "user_ids", DbTypes.Types.String).Value = csv;
                            await db.ExecuteNonQuery(upd);

                            if (ids.Count > 1)
                            {
                                for (int i = 1; i < ids.Count; i++)
                                {
                                    var del = db.GetCommand("UPDATE implementation_assignments SET is_active=false WHERE id=@id;");
                                    db.AddParameter(del, "id", DbTypes.Types.Integer).Value = ids[i];
                                    await db.ExecuteNonQuery(del);
                                }
                            }
                        }

                        await db.CommitTransaction();

                        var entity = new ImplementationAssignment { Id = entityId, ServiceId = serviceId, UserIds = userIds };
                        return new ApiResponse<ImplementationAssignmentDto>
                        {
                            Success = true,
                            Message = "Team assignment saved",
                            Data = MapImplementationAssignment(entity)
                        };
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
                return new ApiResponse<ImplementationAssignmentDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
