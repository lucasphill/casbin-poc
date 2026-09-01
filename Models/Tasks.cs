using System.ComponentModel.DataAnnotations.Schema;

namespace casbin_poc.Models
{
    public class Tasks
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool Status { get; set; } = false;
        public DateTime? DueDate { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public Guid OwnerId { get; set; }
        [ForeignKey("OwnerId")]
        public Users Owner { get; set; }
    }
}
