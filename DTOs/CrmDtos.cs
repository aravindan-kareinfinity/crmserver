namespace CRM.Server.DTOs
{
    // ========== Customer DTOs ==========
    public class CreateCustomerDto
    {
        public string RegName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int? BusinessTypeId { get; set; }
        public int? IndustryId { get; set; }
        /// <summary>FK to reference_entries (category "Lead Source").</summary>
        public int? LeadSourceId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public int? CountryId { get; set; }
        public string Pincode { get; set; } = string.Empty;
        public string? GstNumber { get; set; }
        public List<string> ContactPersons { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<string> Mobiles { get; set; } = new();
        public int ShopSizeId { get; set; }
        public int TierId { get; set; }
        /// <summary>FK to reference_entries (e.g. customer type: lead/prospect/customer).</summary>
        public int TypeId { get; set; }
        /// <summary>Authenticated user performing this create (stored in <c>created_by</c> / <c>modified_by</c> as user id).</summary>
        public long? CreatedByUserId { get; set; }
        public bool? ProductFeaturesDiscussed { get; set; }
        public long? AssignedRepresentativeId { get; set; }
        public int? InteractionModeId { get; set; }
        public bool? PricePlanSelected { get; set; }
        public bool? QuotationPreparedSent { get; set; }
        public bool? QuotationAccepted { get; set; }
        public bool? AdvancePaymentReceived { get; set; }
        public bool? InvoiceGenerated { get; set; }
        public string? InvoiceNumber { get; set; }
    }

    /// <summary>Result of <c>POST /api/Customers/bulk</c> (all-or-nothing import).</summary>
    public class BulkImportCustomersResultDto
    {
        public int ImportedCount { get; set; }
        public List<string> RowErrors { get; set; } = new();
        public List<CustomerResponseDto> Created { get; set; } = new();
    }

    public class UpdateCustomerDto
    {
        public string? RegName { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public int? BusinessTypeId { get; set; }
        public int? IndustryId { get; set; }
        /// <summary>FK to reference_entries (category "Lead Source").</summary>
        public int? LeadSourceId { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public int? CountryId { get; set; }
        public string? Pincode { get; set; }
        public string? GstNumber { get; set; }
        public int? ShopSizeId { get; set; }
        public int? TierId { get; set; }
        public List<string>? ContactPersons { get; set; }
        public List<string>? Emails { get; set; }
        public List<string>? Mobiles { get; set; }
        public int? TypeId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? ConvertedAt { get; set; }
        public string? ConvertedBy { get; set; }
        /// <summary>Prospect pipeline stage (UI: New, Contacted, …). Empty string clears.</summary>
        public string? PipelineStatus { get; set; }
        /// <summary>Authenticated user performing this update (stored in <c>modified_by</c> as user id).</summary>
        public long? ModifiedByUserId { get; set; }
        public bool? ProductFeaturesDiscussed { get; set; }
        public long? AssignedRepresentativeId { get; set; }
        public int? InteractionModeId { get; set; }
        public bool? PricePlanSelected { get; set; }
        public bool? QuotationPreparedSent { get; set; }
        public bool? QuotationAccepted { get; set; }
        public bool? AdvancePaymentReceived { get; set; }
        public bool? InvoiceGenerated { get; set; }
        public string? InvoiceNumber { get; set; }
    }

    /// <summary>Aligned with core-crm-suite <c>Customer</c> (types.ts).</summary>
    public class CustomerResponseDto
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string RegName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int? BusinessTypeId { get; set; }
        public int? IndustryId { get; set; }
        public int? LeadSourceId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public int? CountryId { get; set; }
        public string Pincode { get; set; } = string.Empty;
        public string? GstNumber { get; set; }
        public List<string> ContactPersons { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<string> Mobiles { get; set; } = new();
        public int ShopSizeId { get; set; }
        public int TierId { get; set; }
        public int TypeId { get; set; }
        public bool IsActive { get; set; }
        public int? TotalLocations { get; set; }
        public int? TotalTradeNames { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        /// <summary>Resolved from <c>users</c> when <see cref="CreatedBy"/> matches <c>users.id</c>.</summary>
        public string? CreatedByName { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ConvertedAt { get; set; }
        public string? ConvertedBy { get; set; }
        public DateTime? ProspectConvertedAt { get; set; }
        public long? ProspectConvertedBy { get; set; }
        public string? ProspectConvertedByName { get; set; }
        public DateTime? CustomerConvertedAt { get; set; }
        public long? CustomerConvertedBy { get; set; }
        public string? CustomerConvertedByName { get; set; }
        /// <summary>Prospect pipeline stage for /pipeline kanban.</summary>
        public string? PipelineStatus { get; set; }
        public bool ProductFeaturesDiscussed { get; set; }
        public long? AssignedRepresentativeId { get; set; }
        public int? InteractionModeId { get; set; }
        public bool PricePlanSelected { get; set; }
        public bool QuotationPreparedSent { get; set; }
        public bool QuotationAccepted { get; set; }
        public bool AdvancePaymentReceived { get; set; }
        public bool InvoiceGenerated { get; set; }
        public string? InvoiceNumber { get; set; }
    }

    // ========== Service DTOs ==========
    public class CreateServiceDto
    {
        /// <summary>Numeric FK; optional if <see cref="CustomerCode"/> is set.</summary>
        public int CustomerId { get; set; }
        /// <summary>Stable customer business code (e.g. 2026/0001); takes precedence over <see cref="CustomerId"/> when set.</summary>
        public string? CustomerCode { get; set; }
        public int ServiceTypeId { get; set; }
        public int? FrequencyId { get; set; }
        public DateTime DueDate { get; set; }
        public int? DueMonth { get; set; }
        public decimal? AmcPercentage { get; set; }
        public decimal? AmcAmount { get; set; }
        public bool ImplementationRequired { get; set; }
        /// <summary>1 = Open, 2 = In Progress, 3 = Completed (hardcoded; not reference_entries).</summary>
        public int? ImplementationStatusId { get; set; }
        public string? ProjectTitle { get; set; }
        public int? ProjectManagerId { get; set; }
        public decimal? BudgetAmount { get; set; }
        public string? Notes { get; set; }
        public int? LocationId { get; set; }
        /// <summary>Location <c>Code</c> for this customer; used when resolving link if preferred over <see cref="LocationId"/>.</summary>
        public string? LocationCode { get; set; }
        public int? TradeNameId { get; set; }
        public int? TaxId { get; set; }
        /// <summary>Base amount before tax (tax % from <see cref="TaxId"/> reference).</summary>
        public decimal? ServiceValue { get; set; }
        public DateTime? LiveDate { get; set; }
    }

    public class UpdateServiceDto
    {
        public int? ServiceTypeId { get; set; }
        public int? FrequencyId { get; set; }
        public DateTime? DueDate { get; set; }
        public int? DueMonth { get; set; }
        public decimal? AmcPercentage { get; set; }
        public decimal? AmcAmount { get; set; }
        public bool? ImplementationRequired { get; set; }
        /// <summary>1 = Open, 2 = In Progress, 3 = Completed (hardcoded; not reference_entries).</summary>
        public int? ImplementationStatusId { get; set; }
        /// <summary>Authenticated user performing this update (stored in <c>modified_by</c> as user id).</summary>
        public long? ModifiedByUserId { get; set; }
        public bool? IsActive { get; set; }
        public string? Notes { get; set; }
        /// <summary>When true, applies <see cref="LocationId"/>, <see cref="LocationCode"/>, <see cref="TradeNameId"/>, <see cref="TaxId"/>, <see cref="ServiceValue"/> (including nulls).</summary>
        public bool? UpdateBillingLinks { get; set; }
        public int? LocationId { get; set; }
        /// <summary>When <see cref="UpdateBillingLinks"/> is true, resolves under the service's customer; takes precedence over <see cref="LocationId"/> when set.</summary>
        public string? LocationCode { get; set; }
        public int? TradeNameId { get; set; }
        public int? TaxId { get; set; }
        public decimal? ServiceValue { get; set; }
        /// <summary>Staff user id for project lead; send 0 or negative to clear.</summary>
        public int? ProjectManagerId { get; set; }
        /// <summary>0–100; optional manual implementation progress.</summary>
        public int? ProgressPercentage { get; set; }
        /// <summary>
        /// When true (Services → Implement), stamps <see cref="ServiceResponseDto.ImplementationStartedAt"/> if unset
        /// and sets workflow to Open so the project appears on the implementation board.
        /// </summary>
        public bool? BeginImplementation { get; set; }
        public DateTime? LiveDate { get; set; }
    }

    public class GoLiveServiceDto
    {
        public DateTime LiveDate { get; set; }
        public long? ModifiedByUserId { get; set; }
    }

    public class ServiceResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        /// <summary>Stable customer code from <c>customers.code</c>.</summary>
        public string? CustomerCode { get; set; }
        public int? LocationId { get; set; }
        /// <summary>Location business code from <c>locations.code</c>.</summary>
        public string? LocationCode { get; set; }
        public int? TradeNameId { get; set; }
        public int ServiceTypeId { get; set; }
        public int? FrequencyId { get; set; }
        public DateTime DueDate { get; set; }
        public int DueMonth { get; set; }
        public decimal? AmcPercentage { get; set; }
        public decimal? AmcAmount { get; set; }
        public bool ImplementationRequired { get; set; }
        /// <summary>1 = Open, 2 = In Progress, 3 = Completed (hardcoded; not reference_entries).</summary>
        public int? ImplementationStatusId { get; set; }
        public int? ImplementationStageId { get; set; }
        public DateTime? ImplementationStartedAt { get; set; }
        public string? ImplementationStartedBy { get; set; }
        public DateTime? ImplementationCompletedAt { get; set; }
        public string? ImplementationCompletedBy { get; set; }
        public string? ProjectTitle { get; set; }
        public int? ProjectManagerId { get; set; }
        public int? ProgressPercentage { get; set; }
        public decimal? ServiceValue { get; set; }
        public int? TaxId { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LiveDate { get; set; }
    }

    // ========== Invoice DTOs ==========
    public class CreateInvoiceDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int ServiceId { get; set; }
        public int? StaffId { get; set; }
        public int PaymentModeId { get; set; }
        public int PaymentStatusId { get; set; }
        public decimal Receivable { get; set; }
        public decimal Received { get; set; }
        public DateTime SubscriptionStartAt { get; set; }
        public DateTime SubscriptionEndAt { get; set; }
    }

    /// <summary>Aligned with core-crm-suite <c>Invoice</c>.</summary>
    public class InvoiceResponseDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int ServiceId { get; set; }
        public int? StaffId { get; set; }
        public int PaymentModeId { get; set; }
        public int PaymentStatusId { get; set; }
        public decimal Receivable { get; set; }
        public decimal Received { get; set; }
        public string SubscriptionStartAt { get; set; } = string.Empty;
        public string SubscriptionEndAt { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        /// <summary>Resolved from <c>users</c> when <see cref="CreatedBy"/> matches <c>users.id</c>.</summary>
        public string? CreatedByName { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaidBy { get; set; }
    }

    public class UpdateInvoiceDto
    {
        public int? PaymentStatusId { get; set; }
        public decimal? Receivable { get; set; }
        public decimal? Received { get; set; }
        public DateTime? SubscriptionStartAt { get; set; }
        public DateTime? SubscriptionEndAt { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaidBy { get; set; }
    }

    // ========== Payment DTOs ==========
    public class CollectPaymentDto
    {
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public int PaymentModeId { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? Notes { get; set; }
        /// <summary>Optional user id for attribution (stored as created_by/modified_by on payment; and invoice modified_by).</summary>
        public long? UserId { get; set; }
    }

    public class PaymentResponseDto
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Remaining { get; set; }
        public int PaymentModeId { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
    }

    public class CollectPaymentResultDto
    {
        public PaymentResponseDto Payment { get; set; } = new();
        public InvoiceResponseDto Invoice { get; set; } = new();
    }

    // ========== Ticket DTOs ==========
    public class CreateTicketDto
    {
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int LocationId { get; set; }
        public string? LocationCode { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? ContactMobile { get; set; }
        /// <summary>Ticket status reference id (reference_entries, category: "Ticket Status").</summary>
        public int? StatusId { get; set; }
        public string Priority { get; set; } = "medium";
        public int AssignedTo { get; set; }
        public int CategoryId { get; set; }
        public int? ModuleId { get; set; }
        /// <summary>User id for ticket timeline attribution (optional).</summary>
        public int? ChangedByUserId { get; set; }
    }

    public class UpdateTicketDto
    {
        public int? StatusId { get; set; }
        public string? Priority { get; set; }
        public int? AssignedTo { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int? LocationId { get; set; }
        public string? LocationCode { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactMobile { get; set; }
        public string? Subject { get; set; }
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public int? ModuleId { get; set; }
        public bool? IsActive { get; set; }
        /// <summary>User id for ticket timeline attribution (optional).</summary>
        public int? ChangedByUserId { get; set; }
    }

    /// <summary>Aligned with core-crm-suite <c>Ticket</c>.</summary>
    public class TicketResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int LocationId { get; set; }
        public string? LocationCode { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? ContactMobile { get; set; }
        public int StatusId { get; set; }
        public string Priority { get; set; } = string.Empty;
        public int AssignedTo { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ClosedAt { get; set; }
        public long? ClosedBy { get; set; }
        public int CategoryId { get; set; }
        public int? ModuleId { get; set; }
    }

    // ========== Location DTOs (PostgreSQL: locations) ==========
    public class CreateLocationDto
    {
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RegName { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public int CityId { get; set; }
        public int StateId { get; set; }
        public int CountryId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public List<string> ContactPersons { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<string> Mobiles { get; set; } = new();
        public int ShopSizeId { get; set; }
        public int TierId { get; set; }
        public bool IsPrimary { get; set; }
        public string GstNumber { get; set; } = string.Empty;
        public bool? IsEnabled { get; set; }
    }

    /// <summary>Aligned with core-crm-suite <c>Location</c>.</summary>
    public class LocationResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RegName { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public int CityId { get; set; }
        public int StateId { get; set; }
        public int CountryId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public List<string> ContactPersons { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<string> Mobiles { get; set; } = new();
        public int ShopSizeId { get; set; }
        public int TierId { get; set; }
        public bool IsPrimary { get; set; }
        public string GstNumber { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    // ========== Reference DTOs ==========
    public class ReferenceResponseDto
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public bool? RequiresImplementation { get; set; }
        public bool? IsImplementation { get; set; }
    }

    public class CreateReferenceDto
    {
        public string Category { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public bool? RequiresImplementation { get; set; }
        public bool? IsImplementation { get; set; }
    }

    public class UpdateReferenceDto
    {
        public string Category { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public bool? RequiresImplementation { get; set; }
        public bool? IsImplementation { get; set; }
    }

    public class ReferenceLabelResponseDto
    {
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>Result of <c>GET /api/References/resolve-pincode/{pincode}</c> (India Post API + reference_entries).</summary>
    public class PincodeResolveResponseDto
    {
        public int CountryId { get; set; }
        public int StateId { get; set; }
        public int CityId { get; set; }
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        /// <summary>District from API, stored as <c>City</c> reference label.</summary>
        public string District { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        /// <summary>True if any new rows were inserted into <c>reference_entries</c>.</summary>
        public bool CreatedNewReferences { get; set; }
    }

    // ========== Timeline DTOs (aligned with BaseTimeline in UI) ==========
    public class TimelineEntryDto
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int? FileId { get; set; }
        public string? FileName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public long CreatedBy { get; set; }
        /// <summary>Resolved from <c>users</c> when <see cref="CreatedBy"/> matches <c>users.id</c>.</summary>
        public string? CreatedByName { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    public class CustomerTimelineEntryDto : TimelineEntryDto
    {
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
    }

    public class LocationTimelineEntryDto : TimelineEntryDto
    {
        public int LocationId { get; set; }
    }

    public class InvoiceTimelineEntryDto : TimelineEntryDto
    {
        public int InvoiceId { get; set; }
    }

    public class TicketTimelineEntryDto : TimelineEntryDto
    {
        public int TicketId { get; set; }
        public int UserId { get; set; }
    }

    public class InvestmentTimelineEntryDto : TimelineEntryDto
    {
        public int InvestmentId { get; set; }
    }

    public class AddTimelineEntryDto
    {
        public int Type { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int? FileId { get; set; }
        public string? FileName { get; set; }
    }

    public class AddTicketTimelineEntryDto : AddTimelineEntryDto
    {
        public int UserId { get; set; }
    }

    public class ImplementationTimelineEntryDto : TimelineEntryDto
    {
        public int ServiceId { get; set; }
        /// <summary>1 = Open, 2 = In Progress, 3 = Completed.</summary>
        public int StatusId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int UserId { get; set; }
    }

    public class AddImplementationTimelineEntryDto : AddTimelineEntryDto
    {
        /// <summary>1 = Open, 2 = In Progress, 3 = Completed.</summary>
        public int StatusId { get; set; }
        public int UserId { get; set; }
    }

    public class ImplementationAssignmentDto
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public List<int> UserIds { get; set; } = new();
    }

    public class UpsertImplementationAssignmentDto
    {
        public List<int> UserIds { get; set; } = new();
    }

    // ========== User DTOs (aligned with core-crm-suite types.ts User) ==========

    public class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Successful login payload: numeric user id, login username, refresh token, role.</summary>
    public class LoginResponseDto
    {
        /// <summary>Database primary key (<c>users.id</c>).</summary>
        public int UserId { get; set; }
        /// <summary>Login identifier (<c>users.user_id</c>).</summary>
        public string Username { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        /// <summary>From <c>roles.permissions</c> for <see cref="Role"/> name matching <c>users.role</c>.</summary>
        public List<string> Permissions { get; set; } = new();
    }

    public class UserResponseDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        /// <summary>From <c>roles.permissions</c> for this user's role name.</summary>
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    public class CreateUserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        /// <summary>Plain text; stored as BCrypt hash only.</summary>
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
        /// <summary>If set, replaces password hash.</summary>
        public string? Password { get; set; }
    }

    // ========== Role DTOs (roles table + core-crm-suite RolesPage) ==========
    public class RoleResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    public class CreateRoleDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    public class UpdateRoleDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    // ========== Trademark DTOs ==========
    public class CreateTrademarkDto
    {
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int LocationId { get; set; }
        public string? LocationCode { get; set; }
        public string RegName { get; set; } = string.Empty;
        public string GstNumber { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public int CityId { get; set; }
        public int StateId { get; set; }
        public int? CountryId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public List<string> ContactPersons { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<string> Mobiles { get; set; } = new();
        public int TierId { get; set; }
        public int? ShopSizeId { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
        public bool? IsEnabled { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateTrademarkDto
    {
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int LocationId { get; set; }
        public string? LocationCode { get; set; }
        public string RegName { get; set; } = string.Empty;
        public string GstNumber { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public int CityId { get; set; }
        public int StateId { get; set; }
        public int? CountryId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public List<string> ContactPersons { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<string> Mobiles { get; set; } = new();
        public int TierId { get; set; }
        public int? ShopSizeId { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public bool? IsEnabled { get; set; }
        public string? Remarks { get; set; }
    }

    public class TrademarkResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int LocationId { get; set; }
        public string? LocationCode { get; set; }
        public string RegName { get; set; } = string.Empty;
        public string GstNumber { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public int CityId { get; set; }
        public int StateId { get; set; }
        public int? CountryId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public List<string> ContactPersons { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<string> Mobiles { get; set; } = new();
        public int TierId { get; set; }
        public int? ShopSizeId { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsActive { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    // ========== Scheduler event DTOs ==========
    public class SchedulerEventResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<int> Attendees { get; set; } = new();
        public string? Location { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = "scheduled";
        public bool IsActive { get; set; }
        public string? RelatedToType { get; set; }
        public int? RelatedToId { get; set; }
        public long CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    public class CreateSchedulerEventDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<int> Attendees { get; set; } = new();
        public string? Location { get; set; }
        public string Type { get; set; } = "meeting";
        public string Priority { get; set; } = "medium";
        public string Status { get; set; } = "scheduled";
        public string? RelatedToType { get; set; }
        public int? RelatedToId { get; set; }
    }

    public class UpdateSchedulerEventDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<int> Attendees { get; set; } = new();
        public string? Location { get; set; }
        public string Type { get; set; } = "meeting";
        public string Priority { get; set; } = "medium";
        public string Status { get; set; } = "scheduled";
        public string? RelatedToType { get; set; }
        public int? RelatedToId { get; set; }
    }

    // ========== Investment DTOs ==========
    public class CreateInvestmentDto
    {
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int LocationId { get; set; }
        public string? LocationCode { get; set; }
        public decimal Amount { get; set; }
        public int InvestmentTypeId { get; set; }
        public int? StaffId { get; set; }
        public bool? NeedsClaim { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class UpdateInvestmentDto
    {
        public int? CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int? LocationId { get; set; }
        public string? LocationCode { get; set; }
        public int? InvestmentTypeId { get; set; }
        public decimal? Amount { get; set; }
        public int? StaffId { get; set; }
        /// <summary>When true, clears <see cref="StaffId"/> (ignores <see cref="StaffId"/> value).</summary>
        public bool? StaffIdCleared { get; set; }
        public bool? NeedsClaim { get; set; }
        public string? Notes { get; set; }
        public bool? IsActive { get; set; }
    }

    public class InvestmentResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public int LocationId { get; set; }
        public string? LocationCode { get; set; }
        public decimal Amount { get; set; }
        public decimal ClaimedAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public bool ClaimedFully { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public long? ClaimedBy { get; set; }
        public string? ClaimNotes { get; set; }
        public bool NeedsClaim { get; set; }
        public int InvestmentTypeId { get; set; }
        public int? StaffId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    public class ClaimInvestmentDto
    {
        public int InvestmentId { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public string? Notes { get; set; }
        public long? UserId { get; set; }
    }

    public class InvestmentClaimSummaryDto
    {
        public long? UserId { get; set; }
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class InvestmentClaimRowDto
    {
        public int InvestmentId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public decimal Amount { get; set; }
        public DateTime ClaimedAt { get; set; }
        public long? ClaimedBy { get; set; }
        public string? ClaimNotes { get; set; }
        public int InvestmentTypeId { get; set; }
        public int? StaffId { get; set; }
    }

    // ========== Reports (saved SQL + run with bound dates) ==========

    public class ReportResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public Dictionary<string, string> Filters { get; set; } = new();
        public string? GroupBy { get; set; }
        public string? SortBy { get; set; }
        public string? Query { get; set; }
        public bool IsActive { get; set; }
        public long CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime LastRun { get; set; }
    }

    public class CreateReportDto
    {
        public string Name { get; set; } = string.Empty;
        public string Module { get; set; } = "General";
        public List<string> Columns { get; set; } = new();
        public Dictionary<string, string>? Filters { get; set; }
        public string? GroupBy { get; set; }
        public string? SortBy { get; set; }
        public string? Query { get; set; }
        public bool IsActive { get; set; } = true;
        public long CreatedBy { get; set; } = 1;
    }

    public class UpdateReportDto
    {
        public string Name { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public Dictionary<string, string>? Filters { get; set; }
        public string? GroupBy { get; set; }
        public string? SortBy { get; set; }
        public string? Query { get; set; }
        public bool? IsActive { get; set; }
        public long? ModifiedBy { get; set; }
    }

    /// <summary>Bind <c>@start_date</c> / <c>@end_date</c> (or legacy <c>:start_date</c> / <c>:end_date</c>) in stored SQL.</summary>
    public class RunReportRequestDto
    {
        /// <summary>UTC date (yyyy-MM-dd); start of day used.</summary>
        public string StartDate { get; set; } = string.Empty;
        /// <summary>UTC date (yyyy-MM-dd); end of day used.</summary>
        public string EndDate { get; set; } = string.Empty;
        /// <summary>When set, replaces trailing <c>ORDER BY ...</c> with <c>ORDER BY c.id ASC|DESC</c> (queries must use alias <c>c</c> for customers).</summary>
        public string? OrderCreatedAt { get; set; }
    }

    public class ReportRunResultDto
    {
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
    }

    // ========== File (binary storage, e.g. images) ==========
    public class FileStoredResponseDto
    {
        /// <summary>Primary key in <c>files</c> table — use for URLs and FKs.</summary>
        public long ImageId { get; set; }
        public string? Type { get; set; }
    }

    public class FileMetadataResponseDto
    {
        public long Id { get; set; }
        public string? Type { get; set; }
        public int Version { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public bool IsActive { get; set; }
    }

    // ========== Generic Response ==========
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    }
}
