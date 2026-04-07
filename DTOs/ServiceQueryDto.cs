namespace CRM.Server.DTOs
{
    public class ServiceQueryDto
    {
        public string? Search { get; set; }
        public int? CustomerId { get; set; }
        public int? ServiceTypeId { get; set; }
        public int? ImplementationStatusId { get; set; }
        public int? FrequencyId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public bool? IncludeInactive { get; set; }
    }
}

