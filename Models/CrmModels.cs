using System;
using System.Collections.Generic;
using NpgsqlTypes;

namespace CRM.Server.Models
{
    // ========== Enums (PostgreSQL native types) ==========
    /// <summary>Maps to PostgreSQL implementation_status_enum (UPPERCASE labels; not snake_case).</summary>
    public enum ImplementationWorkflowStatus
    {
        [PgName("OPEN")]
        OPEN,
        [PgName("IN_PROGRESS")]
        IN_PROGRESS,
        [PgName("COMPLETED")]
        COMPLETED
    }

    public enum TicketPriority
    {
        critical,
        high,
        medium,
        low
    }

    // ========== Base Timeline ==========
    public abstract class BaseTimeline
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int? FileId { get; set; }
        public string? FileName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public long CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    // ========== Reference / Lookup ==========
    public class ReferenceEntry
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public bool? RequiresImplementation { get; set; }
        public bool? IsImplementation { get; set; }
    }

    // ========== User & Role ==========
    public class User
    {
        public int Id { get; set; }
        /// <summary>Login / external user identifier (maps to user_id).</summary>
        public string UserLoginId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        /// <summary>BCrypt hash; see <c>04_AlterUsers_AddPasswordHash_PostgreSQL.sql</c>.</summary>
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public int? UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    // ========== Customer ==========
    public class Customer
    {
        public int Id { get; set; }
        /// <summary>Stable business key; child tables keep both <see cref="Id"/> and code.</summary>
        public string Code { get; set; } = string.Empty;
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
        public bool IsActive { get; set; } = true;
        public int? TotalLocations { get; set; }
        public int? TotalTradeNames { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ConvertedAt { get; set; }
        public string? ConvertedBy { get; set; }
        public DateTime? ProspectConvertedAt { get; set; }
        public long? ProspectConvertedBy { get; set; }
        public DateTime? CustomerConvertedAt { get; set; }
        public long? CustomerConvertedBy { get; set; }
        /// <summary>Prospect funnel stage (e.g. New, Contacted) for /pipeline.</summary>
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
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual ReferenceEntry? TypeRef { get; set; }
        public virtual ICollection<Service> Services { get; set; } = new List<Service>();
        public virtual ICollection<CustomerTimeline> Timelines { get; set; } = new List<CustomerTimeline>();
        public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
        public virtual ICollection<Trademark> Trademarks { get; set; } = new List<Trademark>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
        public virtual ICollection<Investment> Investments { get; set; } = new List<Investment>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class CustomerTimeline : BaseTimeline
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public virtual Customer? Customer { get; set; }
    }

    // ========== Service ==========
    public class Service
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public int? LocationId { get; set; }
        public int? TradeNameId { get; set; }
        public int ServiceTypeId { get; set; }
        public int? FrequencyId { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? LiveDate { get; set; }
        public decimal? ServiceValue { get; set; }
        public int DueMonth { get; set; }
        public decimal? AmcPercentage { get; set; }
        public decimal? AmcAmount { get; set; }
        public bool ImplementationRequired { get; set; }
        public ImplementationWorkflowStatus ImplementationStatus { get; set; } = ImplementationWorkflowStatus.OPEN;
        public int? ImplementationStageId { get; set; }
        public DateTime? ImplementationStartedAt { get; set; }
        public string? ImplementationStartedBy { get; set; }
        public DateTime? ImplementationCompletedAt { get; set; }
        public string? ImplementationCompletedBy { get; set; }
        public string? ProjectTitle { get; set; }
        public int? ProjectManagerId { get; set; }
        public decimal? BudgetAmount { get; set; }
        public int? ProgressPercentage { get; set; }
        public int? TaxId { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<ImplementationAssignment> Assignments { get; set; } = new List<ImplementationAssignment>();
        public virtual ICollection<ImplementationTimeline> Timelines { get; set; } = new List<ImplementationTimeline>();
    }

    // ========== Invoice ==========
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public int? StaffId { get; set; }
        public int PaymentModeId { get; set; }
        public int PaymentStatusId { get; set; }
        public decimal Receivable { get; set; }
        public decimal Received { get; set; }
        public DateTime SubscriptionStartAt { get; set; }
        public DateTime SubscriptionEndAt { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaidBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual Service? Service { get; set; }
        public virtual ICollection<InvoiceTimeline> Timelines { get; set; } = new List<InvoiceTimeline>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class InvoiceTimeline : BaseTimeline
    {
        public int InvoiceId { get; set; }
        public virtual Invoice? Invoice { get; set; }
    }

    // ========== Payments ==========
    public class Payment
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
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual Invoice? Invoice { get; set; }
        public virtual Customer? Customer { get; set; }
    }

    // ========== Investment ==========
    public class Investment
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public decimal Amount { get; set; }
        public decimal ClaimedAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public bool ClaimedFully { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public long? ClaimedBy { get; set; }
        public string? ClaimNotes { get; set; }
        public bool NeedsClaim { get; set; } = true;
        public int InvestmentTypeId { get; set; }
        public int? StaffId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual ICollection<InvestmentTimeline> Timelines { get; set; } = new List<InvestmentTimeline>();
    }

    public class InvestmentTimeline : BaseTimeline
    {
        public int InvestmentId { get; set; }
        public virtual Investment? Investment { get; set; }
    }


    // ========== Implementation ==========
    public class ImplementationAssignment
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public List<int> UserIds { get; set; } = new();

        public virtual Service? Service { get; set; }
    }

    public class ImplementationTimeline : BaseTimeline
    {
        public int ServiceId { get; set; }
        public ImplementationWorkflowStatus WorkflowStatus { get; set; }
        public int UserId { get; set; }

        public virtual Service? Service { get; set; }
    }

    // ========== Tickets ==========
    public class Ticket
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? ContactMobile { get; set; }
        /// <summary>Ticket status reference id (<c>reference_entries</c>, category: "Ticket Status").</summary>
        public int StatusId { get; set; }
        public TicketPriority Priority { get; set; }
        public int AssignedTo { get; set; }
        public DateTime SlaDeadline { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ClosedAt { get; set; }
        public long? ClosedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        /// <summary>Ticket category reference id (<c>reference_entries</c>, category: "Ticket Category").</summary>
        public int CategoryId { get; set; }
        /// <summary>Ticket module reference id (<c>reference_entries</c>, category: "Ticket Module").</summary>
        public int? ModuleId { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual ICollection<TicketTimeline> Timelines { get; set; } = new List<TicketTimeline>();
    }

    public class TicketTimeline : BaseTimeline
    {
        public int TicketId { get; set; }
        public int UserId { get; set; }

        public virtual Ticket? Ticket { get; set; }
    }

    // ========== Report ==========
    public class Report
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public Dictionary<string, string> Filters { get; set; } = new();
        public string? GroupBy { get; set; }
        public string? SortBy { get; set; }
        /// <summary>SQL or report definition text (maps to <c>reports.query</c>).</summary>
        public string? Query { get; set; }
        public bool IsActive { get; set; } = true;
        public long CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastRun { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    /// <summary>Stored binary files (e.g. images) in <c>files</c> table.</summary>
    public class CrmFile
    {
        public long Id { get; set; }
        public bool IsFactory { get; set; }
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public int Version { get; set; } = 1;
        public long? CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? Attributes { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSuspended { get; set; }
        public long? ParentId { get; set; }
        public string? Notes { get; set; }
        /// <summary>MIME type, e.g. image/png.</summary>
        public string? Type { get; set; }
    }

    // ========== Scheduler Event ==========
    public class SchedulerEvent
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<int> Attendees { get; set; } = new();
        public string? Location { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = "low";
        /// <summary>scheduled | completed | cancelled (see <c>scheduler_events.status</c>).</summary>
        public string Status { get; set; } = "scheduled";
        public bool IsActive { get; set; } = true;
        public string? RelatedToType { get; set; }
        public int? RelatedToId { get; set; }
        public long CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
    }

    // ========== Trademark ==========
    public class Trademark
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public int LocationId { get; set; }
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
        public bool IsEnabled { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual Location? Location { get; set; }
    }

    // ========== Location (formerly Branch) ==========
    public class Location
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
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
        public bool IsEnabled { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual ICollection<LocationTimeline> Timelines { get; set; } = new List<LocationTimeline>();
    }

    public class LocationTimeline : BaseTimeline
    {
        public int LocationId { get; set; }
        public virtual Location? Location { get; set; }
    }

}
