namespace Pipelane.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "owner";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

