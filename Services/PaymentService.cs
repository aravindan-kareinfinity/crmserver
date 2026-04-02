using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;
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
        IDbProvider dbprovider;
        IQueryBuilderProvider querybuilderprovider;

        public PaymentService(CrmDbContext context, IDbProvider dbprovider, IQueryBuilderProvider querybuilderprovider)
        {
            this.context = context;
            this.dbprovider = dbprovider;
            this.querybuilderprovider = querybuilderprovider;
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
                List<PaymentResponseDto> rows = new List<PaymentResponseDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    // SQL-first implementation (SQL string + QueryBuilder)
                    // Matches EF query:
                    //   WHERE invoice_id = {invoiceId} AND is_active = true
                    //   ORDER BY received_at DESC
                    string query = @"
SELECT
    id,
    invoice_id,
    customer_code,
    amount,
    remaining,
    payment_mode_id,
    received_at,
    notes,
    created_at,
    created_by
FROM payments";

                    var queryBuilder = querybuilderprovider.GetQueryBuilder(query);
                    queryBuilder.AddParameter("invoice_id", "=", "invoice_id", invoiceId, DbTypes.Types.Integer);
                    queryBuilder.AddParameter("is_active", "=", "is_active", true, DbTypes.Types.Boolean);
                    queryBuilder.AddOrderBy(QueryBuilder.Order.DESC, "received_at");

                    var command = queryBuilder.GetCommand(db);
                    using (var reader = await db.Execute(command))
                    {
                        while (await reader.ReadAsync())
                        {
                            rows.Add(new PaymentResponseDto
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                InvoiceId = reader.GetInt32(reader.GetOrdinal("invoice_id")),
                                CustomerCode = reader.GetString(reader.GetOrdinal("customer_code")),
                                Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                                Remaining = reader.GetDecimal(reader.GetOrdinal("remaining")),
                                PaymentModeId = reader.GetInt32(reader.GetOrdinal("payment_mode_id")),
                                ReceivedAt = reader.GetDateTime(reader.GetOrdinal("received_at")),
                                Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                            });
                        }
                    }
                }

                return new ApiResponse<List<PaymentResponseDto>>
                {
                    Success = true,
                    Data = rows
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

            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                await db.BeginTransaction();
                try
                {
                    // 1) Load invoice + customer (for MapInvoice.CustomerId)
                    string invSql = @"
SELECT
    i.id,
    i.invoice_number,
    i.customer_code,
    c.id as customer_id,
    i.service_id,
    i.staff_id,
    i.payment_mode_id,
    i.payment_status_id,
    i.receivable,
    i.received,
    i.subscription_start_at,
    i.subscription_end_at,
    i.is_active,
    i.created_at,
    i.created_by,
    i.paid_at,
    i.paid_by,
    i.modified_at,
    i.modified_by
FROM invoices i
LEFT JOIN customers c ON c.code = i.customer_code
WHERE i.id = @invoice_id
LIMIT 1;";

                    var invCmd = db.GetCommand(invSql);
                    db.AddParameter(invCmd, "invoice_id", DbTypes.Types.Integer).Value = dto.InvoiceId;

                    Invoice? inv = null;
                    using (DbDataReader invReader = await db.Execute(invCmd))
                    {
                        if (!await invReader.ReadAsync())
                        {
                            await db.RollbackTransaction();
                            return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = "Invoice not found" };
                        }

                        inv = new Invoice
                        {
                            Id = invReader.GetInt32(invReader.GetOrdinal("id")),
                            InvoiceNumber = invReader.GetString(invReader.GetOrdinal("invoice_number")),
                            CustomerCode = invReader.GetString(invReader.GetOrdinal("customer_code")),
                            Customer = new Customer
                            {
                                Id = invReader.IsDBNull(invReader.GetOrdinal("customer_id")) ? 0 : invReader.GetInt32(invReader.GetOrdinal("customer_id"))
                            },
                            ServiceId = invReader.GetInt32(invReader.GetOrdinal("service_id")),
                            StaffId = invReader.IsDBNull(invReader.GetOrdinal("staff_id")) ? null : invReader.GetInt32(invReader.GetOrdinal("staff_id")),
                            PaymentModeId = invReader.GetInt32(invReader.GetOrdinal("payment_mode_id")),
                            PaymentStatusId = invReader.GetInt32(invReader.GetOrdinal("payment_status_id")),
                            Receivable = invReader.GetDecimal(invReader.GetOrdinal("receivable")),
                            Received = invReader.GetDecimal(invReader.GetOrdinal("received")),
                            SubscriptionStartAt = invReader.GetDateTime(invReader.GetOrdinal("subscription_start_at")),
                            SubscriptionEndAt = invReader.GetDateTime(invReader.GetOrdinal("subscription_end_at")),
                            IsActive = invReader.GetBoolean(invReader.GetOrdinal("is_active")),
                            CreatedAt = invReader.GetDateTime(invReader.GetOrdinal("created_at")),
                            CreatedBy = invReader.IsDBNull(invReader.GetOrdinal("created_by")) ? null : invReader.GetInt64(invReader.GetOrdinal("created_by")),
                            PaidAt = invReader.IsDBNull(invReader.GetOrdinal("paid_at")) ? null : invReader.GetDateTime(invReader.GetOrdinal("paid_at")),
                            PaidBy = invReader.IsDBNull(invReader.GetOrdinal("paid_by")) ? null : invReader.GetString(invReader.GetOrdinal("paid_by")),
                            ModifiedAt = invReader.GetDateTime(invReader.GetOrdinal("modified_at")),
                            ModifiedBy = invReader.IsDBNull(invReader.GetOrdinal("modified_by")) ? null : invReader.GetInt64(invReader.GetOrdinal("modified_by")),
                        };
                    }

                    if (inv == null)
                    {
                        await db.RollbackTransaction();
                        return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = "Invoice not found" };
                    }

                    // 2) Compute invoice update (same logic as EF version)
                    var now = DateTime.UtcNow;
                    var receivedAt = dto.ReceivedAt ?? now;
                    if (receivedAt.Kind == DateTimeKind.Unspecified)
                        receivedAt = DateTime.SpecifyKind(receivedAt, DateTimeKind.Utc);

                    var auditUserId = dto.UserId.HasValue && dto.UserId.Value > 0 ? dto.UserId.Value : AuditUserIds.System;

                    var receivable = inv.Receivable < 0 ? 0 : inv.Receivable;
                    var currentReceived = inv.Received < 0 ? 0 : inv.Received;
                    var nextReceived = currentReceived + dto.Amount;
                    if (nextReceived > receivable) nextReceived = receivable;
                    var remaining = receivable - nextReceived;
                    if (remaining < 0) remaining = 0;

                    inv.Received = nextReceived;
                    inv.PaymentModeId = dto.PaymentModeId;

                    int paidStatusId = 0;
                    int pendingStatusId = 0;
                    {
                        string paidSql = @"SELECT id FROM reference_entries WHERE category='Payment Status' AND value='paid' AND is_active=true LIMIT 1;";
                        var paidCmd = db.GetCommand(paidSql);
                        using (DbDataReader paidReader = await db.Execute(paidCmd))
                        {
                            if (await paidReader.ReadAsync() && !paidReader.IsDBNull(paidReader.GetOrdinal("id")))
                                paidStatusId = paidReader.GetInt32(paidReader.GetOrdinal("id"));
                        }

                        string pendingSql = @"SELECT id FROM reference_entries WHERE category='Payment Status' AND value='pending' AND is_active=true LIMIT 1;";
                        var pendingCmd = db.GetCommand(pendingSql);
                        using (DbDataReader pendingReader = await db.Execute(pendingCmd))
                        {
                            if (await pendingReader.ReadAsync() && !pendingReader.IsDBNull(pendingReader.GetOrdinal("id")))
                                pendingStatusId = pendingReader.GetInt32(pendingReader.GetOrdinal("id"));
                        }
                    }

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
                        if (pendingStatusId > 0) inv.PaymentStatusId = pendingStatusId;
                        inv.PaidAt = null;
                        inv.PaidBy = null;
                    }

                    inv.ModifiedAt = now;
                    inv.ModifiedBy = auditUserId;

                    // 3) Update invoice
                    string updateInvSql = @"
UPDATE invoices
SET
    received = @received,
    payment_mode_id = @payment_mode_id,
    payment_status_id = @payment_status_id,
    paid_at = @paid_at,
    paid_by = @paid_by,
    modified_at = @modified_at,
    modified_by = @modified_by
WHERE id = @invoice_id;";

                    var updateInvCmd = db.GetCommand(updateInvSql);
                    db.AddParameter(updateInvCmd, "received", DbTypes.Types.Decimal).Value = inv.Received;
                    db.AddParameter(updateInvCmd, "payment_mode_id", DbTypes.Types.Integer).Value = inv.PaymentModeId;
                    db.AddParameter(updateInvCmd, "payment_status_id", DbTypes.Types.Integer).Value = inv.PaymentStatusId;
                    db.AddParameter(updateInvCmd, "paid_at", DbTypes.Types.DateTime).Value = inv.PaidAt.HasValue ? inv.PaidAt.Value : DBNull.Value;
                    db.AddParameter(updateInvCmd, "paid_by", DbTypes.Types.String).Value = inv.PaidBy ?? (object)DBNull.Value;
                    db.AddParameter(updateInvCmd, "modified_at", DbTypes.Types.DateTime).Value = inv.ModifiedAt;
                    db.AddParameter(updateInvCmd, "modified_by", DbTypes.Types.Long).Value = auditUserId;
                    db.AddParameter(updateInvCmd, "invoice_id", DbTypes.Types.Integer).Value = inv.Id;
                    await db.ExecuteNonQuery(updateInvCmd);

                    // 4) Insert payment row
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

                    string insertPaymentSql = @"
INSERT INTO payments (
    invoice_id,
    customer_code,
    amount,
    remaining,
    payment_mode_id,
    received_at,
    notes,
    is_active,
    created_at,
    created_by,
    modified_at,
    modified_by
)
VALUES (
    @invoice_id,
    @customer_code,
    @amount,
    @remaining,
    @payment_mode_id,
    @received_at,
    @notes,
    true,
    @created_at,
    @created_by,
    @modified_at,
    @modified_by
)
RETURNING id;";

                    var insertPaymentCmd = db.GetCommand(insertPaymentSql);
                    db.AddParameter(insertPaymentCmd, "invoice_id", DbTypes.Types.Integer).Value = payment.InvoiceId;
                    db.AddParameter(insertPaymentCmd, "customer_code", DbTypes.Types.String).Value = payment.CustomerCode;
                    db.AddParameter(insertPaymentCmd, "amount", DbTypes.Types.Decimal).Value = payment.Amount;
                    db.AddParameter(insertPaymentCmd, "remaining", DbTypes.Types.Decimal).Value = payment.Remaining;
                    db.AddParameter(insertPaymentCmd, "payment_mode_id", DbTypes.Types.Integer).Value = payment.PaymentModeId;
                    db.AddParameter(insertPaymentCmd, "received_at", DbTypes.Types.DateTime).Value = payment.ReceivedAt;
                    db.AddParameter(insertPaymentCmd, "notes", DbTypes.Types.String).Value = payment.Notes ?? (object)DBNull.Value;
                    db.AddParameter(insertPaymentCmd, "created_at", DbTypes.Types.DateTime).Value = payment.CreatedAt;
                    db.AddParameter(insertPaymentCmd, "created_by", DbTypes.Types.Long).Value = auditUserId;
                    db.AddParameter(insertPaymentCmd, "modified_at", DbTypes.Types.DateTime).Value = payment.ModifiedAt;
                    db.AddParameter(insertPaymentCmd, "modified_by", DbTypes.Types.Long).Value = auditUserId;

                    using (DbDataReader payReader = await db.Execute(insertPaymentCmd))
                    {
                        if (await payReader.ReadAsync())
                            payment.Id = payReader.GetInt32(payReader.GetOrdinal("id"));
                    }

                    // 5) Insert invoice timeline entry
                    string insertTimelineSql = @"
INSERT INTO invoice_timelines (
    invoice_id,
    type,
    notes,
    is_active,
    created_at,
    created_by,
    modified_at,
    modified_by
)
VALUES (
    @invoice_id,
    1,
    @notes,
    true,
    @created_at,
    @created_by,
    @modified_at,
    @modified_by
);";

                    var insertTimelineCmd = db.GetCommand(insertTimelineSql);
                    db.AddParameter(insertTimelineCmd, "invoice_id", DbTypes.Types.Integer).Value = inv.Id;
                    db.AddParameter(insertTimelineCmd, "notes", DbTypes.Types.String).Value = $"Payment collected: {dto.Amount} | Remaining: {remaining}";
                    db.AddParameter(insertTimelineCmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(insertTimelineCmd, "created_by", DbTypes.Types.Long).Value = auditUserId;
                    db.AddParameter(insertTimelineCmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(insertTimelineCmd, "modified_by", DbTypes.Types.Long).Value = auditUserId;
                    await db.ExecuteNonQuery(insertTimelineCmd);

                    // 6) Auto-generate next invoice when fully paid (same conditions)
                    if (reciénPagado)
                    {
                        string serviceSql = @"SELECT id, service_value, frequency_id, tax_id FROM services WHERE id=@service_id LIMIT 1;";
                        var serviceCmd = db.GetCommand(serviceSql);
                        db.AddParameter(serviceCmd, "service_id", DbTypes.Types.Integer).Value = inv.ServiceId;

                        int serviceId = 0;
                        decimal? serviceValue = null;
                        int? frequencyId = null;
                        int? taxId = null;

                        using (DbDataReader svcReader = await db.Execute(serviceCmd))
                        {
                            if (await svcReader.ReadAsync())
                            {
                                serviceId = svcReader.GetInt32(svcReader.GetOrdinal("id"));
                                serviceValue = svcReader.IsDBNull(svcReader.GetOrdinal("service_value")) ? null : svcReader.GetDecimal(svcReader.GetOrdinal("service_value"));
                                frequencyId = svcReader.IsDBNull(svcReader.GetOrdinal("frequency_id")) ? null : svcReader.GetInt32(svcReader.GetOrdinal("frequency_id"));
                                taxId = svcReader.IsDBNull(svcReader.GetOrdinal("tax_id")) ? null : svcReader.GetInt32(svcReader.GetOrdinal("tax_id"));
                            }
                        }

                        if (serviceValue.HasValue && serviceValue.Value > 0 && frequencyId.HasValue)
                        {
                            // frequency label
                            string freqSql = @"SELECT label FROM reference_entries WHERE id=@id LIMIT 1;";
                            var freqCmd = db.GetCommand(freqSql);
                            db.AddParameter(freqCmd, "id", DbTypes.Types.Integer).Value = frequencyId.Value;

                            string? freqLabel = null;
                            using (DbDataReader freqReader = await db.Execute(freqCmd))
                            {
                                if (await freqReader.ReadAsync() && !freqReader.IsDBNull(freqReader.GetOrdinal("label")))
                                    freqLabel = freqReader.GetString(freqReader.GetOrdinal("label"));
                            }

                            if (!string.IsNullOrEmpty(freqLabel))
                            {
                                var flabel = freqLabel.ToLower();
                                bool isCyclic = flabel.Contains("month") || flabel.Contains("quarter") || flabel.Contains("half") || flabel.Contains("year");
                                if (isCyclic)
                                {
                                    // existingUnpaid = AnyAsync(i => i.ServiceId == service.Id && i.Id != inv.Id && i.Received <= 0)
                                    string existingUnpaidSql = @"
SELECT EXISTS (
    SELECT 1 FROM invoices
    WHERE service_id = @service_id
      AND id <> @invoice_id
      AND received <= 0
    LIMIT 1
) as has_unpaid;";

                                    var existingCmd = db.GetCommand(existingUnpaidSql);
                                    db.AddParameter(existingCmd, "service_id", DbTypes.Types.Integer).Value = serviceId;
                                    db.AddParameter(existingCmd, "invoice_id", DbTypes.Types.Integer).Value = inv.Id;

                                    bool existingUnpaid = false;
                                    using (DbDataReader exReader = await db.Execute(existingCmd))
                                    {
                                        if (await exReader.ReadAsync())
                                            existingUnpaid = exReader.GetBoolean(exReader.GetOrdinal("has_unpaid"));
                                    }

                                    if (!existingUnpaid)
                                    {
                                        ReferenceEntry? taxEntry = null;
                                        if (taxId.HasValue)
                                        {
                                            string taxSql = @"SELECT value, label FROM reference_entries WHERE id=@id LIMIT 1;";
                                            var taxCmd = db.GetCommand(taxSql);
                                            db.AddParameter(taxCmd, "id", DbTypes.Types.Integer).Value = taxId.Value;

                                            using (DbDataReader taxReader = await db.Execute(taxCmd))
                                            {
                                                if (await taxReader.ReadAsync())
                                                {
                                                    taxEntry = new ReferenceEntry
                                                    {
                                                        Value = taxReader.IsDBNull(taxReader.GetOrdinal("value")) ? "" : taxReader.GetString(taxReader.GetOrdinal("value")),
                                                        Label = taxReader.IsDBNull(taxReader.GetOrdinal("label")) ? "" : taxReader.GetString(taxReader.GetOrdinal("label"))
                                                    };
                                                }
                                            }
                                        }

                                        var newReceivable = ComputeReceivable(serviceValue.Value, ResolveTaxPercent(taxEntry));

                                        var nextStart = DateTime.SpecifyKind(inv.SubscriptionEndAt, DateTimeKind.Utc);
                                        var nextEnd = nextStart.AddYears(1);
                                        if (flabel.Contains("month")) nextEnd = nextStart.AddMonths(1);
                                        else if (flabel.Contains("quarter")) nextEnd = nextStart.AddMonths(3);
                                        else if (flabel.Contains("half")) nextEnd = nextStart.AddMonths(6);

                                        var newInvoiceNumber = $"INV-S{serviceId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                                        var invoiceNow = DateTime.UtcNow;

                                        // Insert new invoice, RETURNING id for invoice timeline
                                        string insertInvoiceSql = @"
INSERT INTO invoices (
    invoice_number,
    customer_code,
    service_id,
    staff_id,
    payment_mode_id,
    payment_status_id,
    receivable,
    received,
    subscription_start_at,
    subscription_end_at,
    is_active,
    created_at,
    created_by,
    modified_at,
    modified_by
)
VALUES (
    @invoice_number,
    @customer_code,
    @service_id,
    @staff_id,
    @payment_mode_id,
    @payment_status_id,
    @receivable,
    0,
    @subscription_start_at,
    @subscription_end_at,
    true,
    @created_at,
    @created_by,
    @modified_at,
    @modified_by
)
RETURNING id;";

                                        var insertInvCmd = db.GetCommand(insertInvoiceSql);
                                        db.AddParameter(insertInvCmd, "invoice_number", DbTypes.Types.String).Value = newInvoiceNumber;
                                        db.AddParameter(insertInvCmd, "customer_code", DbTypes.Types.String).Value = inv.CustomerCode;
                                        db.AddParameter(insertInvCmd, "service_id", DbTypes.Types.Integer).Value = serviceId;
                                        db.AddParameter(insertInvCmd, "staff_id", DbTypes.Types.Integer).Value = inv.StaffId.HasValue ? inv.StaffId.Value : DBNull.Value;
                                        db.AddParameter(insertInvCmd, "payment_mode_id", DbTypes.Types.Integer).Value = inv.PaymentModeId;
                                        db.AddParameter(insertInvCmd, "payment_status_id", DbTypes.Types.Integer).Value = pendingStatusId;
                                        db.AddParameter(insertInvCmd, "receivable", DbTypes.Types.Decimal).Value = newReceivable;
                                        db.AddParameter(insertInvCmd, "subscription_start_at", DbTypes.Types.DateTime).Value = nextStart;
                                        db.AddParameter(insertInvCmd, "subscription_end_at", DbTypes.Types.DateTime).Value = nextEnd;
                                        db.AddParameter(insertInvCmd, "created_at", DbTypes.Types.DateTime).Value = invoiceNow;
                                        db.AddParameter(insertInvCmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                                        db.AddParameter(insertInvCmd, "modified_at", DbTypes.Types.DateTime).Value = invoiceNow;
                                        db.AddParameter(insertInvCmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                                        int newInvoiceId = 0;
                                        using (DbDataReader newInvReader = await db.Execute(insertInvCmd))
                                        {
                                            if (await newInvReader.ReadAsync())
                                                newInvoiceId = newInvReader.GetInt32(newInvReader.GetOrdinal("id"));
                                        }

                                        // Insert timeline for new invoice
                                        string insertNewTimelineSql = @"
INSERT INTO invoice_timelines (
    invoice_id,
    type,
    notes,
    is_active,
    created_at,
    created_by,
    modified_at,
    modified_by
)
VALUES (
    @invoice_id,
    1,
    @notes,
    true,
    @created_at,
    @created_by,
    @modified_at,
    @modified_by
);";

                                        var insertNewTimelineCmd = db.GetCommand(insertNewTimelineSql);
                                        db.AddParameter(insertNewTimelineCmd, "invoice_id", DbTypes.Types.Integer).Value = newInvoiceId;
                                        db.AddParameter(insertNewTimelineCmd, "notes", DbTypes.Types.String).Value = $"Auto-generated after previous invoice fully paid. Start: {nextStart:yyyy-MM-dd}";
                                        db.AddParameter(insertNewTimelineCmd, "created_at", DbTypes.Types.DateTime).Value = invoiceNow;
                                        db.AddParameter(insertNewTimelineCmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                                        db.AddParameter(insertNewTimelineCmd, "modified_at", DbTypes.Types.DateTime).Value = invoiceNow;
                                        db.AddParameter(insertNewTimelineCmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                                        await db.ExecuteNonQuery(insertNewTimelineCmd);
                                    }
                                }
                            }
                        }
                    }

                    await db.CommitTransaction();

                    var result = new CollectPaymentResultDto
                    {
                        Payment = MapPayment(payment),
                        Invoice = MapInvoice(inv)
                    };
                    return new ApiResponse<CollectPaymentResultDto> { Success = true, Data = result };
                }
                catch (Exception ex)
                {
                    await db.RollbackTransaction();
                    return new ApiResponse<CollectPaymentResultDto> { Success = false, Message = ex.Message };
                }
            }
        }
    }
}

