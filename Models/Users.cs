namespace casbin_poc.Models
{
    public class Users
    {
        public Guid Id { get; set; } 
        public string? Auth0Sub { get; set; } // um usuário poderá nao ter feito login ainda.
        public string? Name { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
