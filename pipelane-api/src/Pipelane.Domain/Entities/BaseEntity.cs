namespace Pipelane.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
}

