using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public interface IPaymentService
    {
        Task<ApiResponse<CollectPaymentResultDto>> Collect(CollectPaymentDto dto);
        Task<ApiResponse<List<PaymentResponseDto>>> GetByInvoice(int invoiceId);
    }

    public class PaymentService : IPaymentService
    {
        CrmDbContext context;

        public PaymentService(CrmDbContext context)
        {
            this.context = context;
        }

        private static PaymentResponseDto MapPayment(Payment p) => new()
        {
            Id = p.Id,
            InvoiceId = p.InvoiceId,
            CustomerCode = p.CustomerCode,
            Amount = p.Amount,
            Remaining = p.Remaining,
            PaymentModeId = p.PaymentModeId,
            ReceivedAt = p.ReceivedAt,
            Notes = p.Notes,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy
        };

        private static string NormalizeInfinity(DateTime dt)
        {
            if (dt == DateTime.MinValue || dt == DateTime.MaxValue) return "";
            return dt.ToString("O");
        }

        private static InvoiceResponseDto MapInvoice(Invoice i) => new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            CustomerId = i.Customer?.Id ?? 0,
            CustomerCode = i.CustomerCode,
            ServiceId = i.ServiceId,
            StaffId = i.StaffId,
            PaymentModeId = i.PaymentModeId,
            PaymentStatusId = i.PaymentStatusId,
            Receivable = i.Receivable,
            Received = i.Received,
            SubscriptionStartAt = NormalizeInfinity(i.SubscriptionStartAt),
            SubscriptionEndAt = NormalizeInfinity(i.SubscriptionEndAt),
            IsActive = i.IsActive,
            CreatedAt = i.CreatedAt,
            CreatedBy = i.CreatedBy,
            ModifiedAt = i.ModifiedAt,
            ModifiedBy = i.ModifiedBy,
            PaidAt = i.PaidAt,
            PaidBy = i.PaidBy
        };

        public async Task<ApiResponse<List<PaymentResponseDto>>> GetByInvoice(int invoiceId)
        {
            try
            {
                var rows = await context.Payments.AsNoTracking()
                    .Where(p => p.InvoiceId == invoiceId && p.IsActive)
                    .OrderByDescending(p => p.ReceivedAt)
                    .ToListAsync();
                return new ApiResponse<List<PaymentResponseDto>>
                {
                    Success = true,
                    Data = rows.Select(MapPayment).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<PaymentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        private static decimal ResolveTaxPercent(ReferenceEntry? tax)
        {
            if (tax == null) return 0;
            var v = (tax.Value ?? "").Trim();
            if (string.Equals(v, "no_tax", StringComparison.OrdinalIgnoreCase)) return 0;
            if (v.StartsWith("gst_", StringComparison.OrdinalIgnoreCase) && v.Length > 4)
            {
                var suffix = v[4..].Replace('_', '.');
                if (decimal.TryParse(suffix, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p))
                    return p;
            }

            var m = System.Text.RegularExpressions.Regex.Match(tax.Label ?? "", @"(\d+(?:\.\d+)?)\s*%");
            if (m.Success &&
                decimal.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var labelPct))
                return labelPct;
            return 0;
        }

        private static decimal ComputeReceivable(decimal baseAmount, decimal taxPercent) =>
            Math.Round(baseAmount + baseAmount * (taxPercent / 100m), 2, MidpointRounding.AwayFromZero);


        public async Task<ApiResponse<CollectPaymentResultDto>> Collect(CollectPaymentDto dto)
        {
            if (dto.InvoiceId <= 0)
                return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = "invoiceId is required" };
            if (dto.PaymentModeId <= 0)
                return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = "paymentModeId is required" };
            if (dto.Amount <= 0)
                return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = "amount must be > 0" };

            await using var tx = await context.Database.BeginTransactionAsync();
            try
            {
                var inv = await context.Invoices
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.Id == dto.InvoiceId);
                if (inv == null)
                    return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = "Invoice not found" };

                var now = DateTime.UtcNow;
                var receivedAt = dto.ReceivedAt ?? now;
                if (receivedAt.Kind == DateTimeKind.Unspecified)
                {
                    receivedAt = DateTime.SpecifyKind(receivedAt, DateTimeKind.Utc);
                }
                var auditUserId = dto.UserId.HasValue && dto.UserId.Value > 0 ? dto.UserId.Value : AuditUserIds.System;

                // Update invoice received and status
                var receivable = inv.Receivable < 0 ? 0 : inv.Receivable;
                var currentReceived = inv.Received < 0 ? 0 : inv.Received;
                var nextReceived = currentReceived + dto.Amount;
                if (nextReceived > receivable) nextReceived = receivable;
                var remaining = receivable - nextReceived;
                if (remaining < 0) remaining = 0;

                inv.Received = nextReceived;
                inv.PaymentModeId = dto.PaymentModeId;

                // Resolve paid status id from reference_entries
                var paidStatusId = await context.ReferenceEntries.AsNoTracking()
                    .Where(r => r.Category == "Payment Status" && r.Value == "paid" && r.IsActive)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();
                var pendingStatusId = await context.ReferenceEntries.AsNoTracking()
                    .Where(r => r.Category == "Payment Status" && r.Value == "pending" && r.IsActive)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                var wasPaid = paidStatusId > 0 && inv.PaymentStatusId == paidStatusId;
                bool reciénPagado = false;
                if (remaining == 0 && receivable > 0 && paidStatusId > 0)
                {
                    inv.PaymentStatusId = paidStatusId;
                    if (!wasPaid)
                    {
                        inv.PaidAt = now;
                        inv.PaidBy = $"User#{auditUserId}";
                        reciénPagado = true;
                    }
                }
                else if (paidStatusId > 0 && inv.PaymentStatusId == paidStatusId && remaining > 0)
                {
                    // If it was paid but now not fully paid, revert to pending (if available)
                    if (pendingStatusId > 0) inv.PaymentStatusId = pendingStatusId;
                    inv.PaidAt = null;
                    inv.PaidBy = null;
                }

                inv.ModifiedAt = now;
                inv.ModifiedBy = auditUserId;

                // Insert payment row
                var payment = new Payment
                {
                    InvoiceId = inv.Id,
                    CustomerCode = inv.CustomerCode,
                    Amount = dto.Amount,
                    Remaining = remaining,
                    PaymentModeId = dto.PaymentModeId,
                    ReceivedAt = receivedAt,
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = auditUserId,
                    ModifiedAt = now,
                    ModifiedBy = auditUserId
                };
                context.Payments.Add(payment);

                // Invoice timeline entry
                context.InvoiceTimelines.Add(new InvoiceTimeline
                {
                    InvoiceId = inv.Id,
                    Type = 1,
                    Notes = $"Payment collected: {dto.Amount} | Remaining: {remaining}",
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = auditUserId,
                    ModifiedAt = now,
                    ModifiedBy = auditUserId
                });

                if (reciénPagado)
                {
                    var service = await context.Services.FirstOrDefaultAsync(s => s.Id == inv.ServiceId);
                    if (service != null && service.ServiceValue > 0)
                    {
                        var freq = service.FrequencyId.HasValue 
                            ? await context.ReferenceEntries.AsNoTracking().FirstOrDefaultAsync(r => r.Id == service.FrequencyId.Value) 
                            : null;
                        
                        if (freq != null && !string.IsNullOrEmpty(freq.Label))
                        {
                            var flabel = freq.Label.ToLower();
                            bool isCyclic = flabel.Contains("month") || flabel.Contains("quarter") || flabel.Contains("half") || flabel.Contains("year");
                            if (isCyclic)
                            {
                                var existingUnpaid = await context.Invoices.AnyAsync(i => i.ServiceId == service.Id && i.Id != inv.Id && i.Received <= 0);
                                if (!existingUnpaid)
                                {
                                    var taxEntry = service.TaxId.HasValue ? await context.ReferenceEntries.AsNoTracking().FirstOrDefaultAsync(r => r.Id == service.TaxId.Value) : null;
                                    var newReceivable = ComputeReceivable(service.ServiceValue.Value, ResolveTaxPercent(taxEntry));
                                    
                                    var nextStart = DateTime.SpecifyKind(inv.SubscriptionEndAt, DateTimeKind.Utc);
                                    var nextEnd = nextStart.AddYears(1);
                                    if (flabel.Contains("month")) nextEnd = nextStart.AddMonths(1);
                                    else if (flabel.Contains("quarter")) nextEnd = nextStart.AddMonths(3);
                                    else if (flabel.Contains("half")) nextEnd = nextStart.AddMonths(6);

                                    var newInv = new Invoice
                                    {
                                        InvoiceNumber = $"INV-S{service.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                                        CustomerCode = inv.CustomerCode,
                                        ServiceId = service.Id,
                                        StaffId = inv.StaffId,
                                        PaymentModeId = inv.PaymentModeId,
                                        PaymentStatusId = pendingStatusId,
                                        Receivable = newReceivable,
                                        Received = 0,
                                        SubscriptionStartAt = nextStart,
                                        SubscriptionEndAt = nextEnd,
                                        IsActive = true,
                                        CreatedAt = DateTime.UtcNow,
                                        CreatedBy = AuditUserIds.System,
                                        ModifiedAt = DateTime.UtcNow,
                                        ModifiedBy = AuditUserIds.System
                                    };
                                    context.Invoices.Add(newInv);

                                    context.InvoiceTimelines.Add(new InvoiceTimeline
                                    {
                                        InvoiceId = newInv.Id,
                                        Type = 1,
                                        Notes = $"Auto-generated after previous invoice fully paid. Start: {nextStart:yyyy-MM-dd}",
                                        IsActive = true,
                                        CreatedAt = DateTime.UtcNow,
                                        CreatedBy = AuditUserIds.System,
                                        ModifiedAt = DateTime.UtcNow,
                                        ModifiedBy = AuditUserIds.System,
                                        Invoice = newInv
                                    });
                                }
                            }
                        }
                    }
                }

                await context.SaveChangesAsync();
                await tx.CommitAsync();

                var result = new CollectPaymentResultDto
                {
                    Payment = MapPayment(payment),
                    Invoice = MapInvoice(inv)
                };
                return new ApiResponse<CollectPaymentResultDto> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = ex.Message };
            }
        }
    }
}

