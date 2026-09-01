namespace casbin_poc.DTO
{
    public class TaskDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool Status { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime Timestamp { get; set; }
        public string Permission { get; set; } = string.Empty;
    }
}
