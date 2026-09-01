namespace casbin_poc.DTO
{
    public class ShareTaskDto
    {
        public Guid TaskId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public ActionType Action { get; set; }
    }
}
