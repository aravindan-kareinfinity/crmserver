namespace CRM.Server.DTOs
{
    public class TicketQueryDto
    {
        public string? Search { get; set; }
        public int? StatusId { get; set; }
        public string? Priority { get; set; }
        public int? CustomerId { get; set; }
        public int? AssignedTo { get; set; }
        public int? CategoryId { get; set; }
        public int? ModuleId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public bool? IncludeInactive { get; set; }
    }
}

