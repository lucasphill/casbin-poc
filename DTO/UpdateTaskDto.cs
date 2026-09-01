namespace casbin_poc.DTO
{
    public class UpdateTaskDto
    {
        public string Title { get; set; } = string.Empty;
        public bool Status { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
