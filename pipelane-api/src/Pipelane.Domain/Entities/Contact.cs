using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class Contact : BaseEntity
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Lang { get; set; }
    public string? TagsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

