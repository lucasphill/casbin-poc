namespace casbin_poc.DTO
{
    public class CreateTaskDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
    }
}
