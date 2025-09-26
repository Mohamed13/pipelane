namespace Pipelane.Application.Common;

public interface ITenantContext
{
    Guid TenantId { get; }
}

public sealed class TenantContext : ITenantContext
{
    public TenantContext(Guid tenantId) => TenantId = tenantId;
    public Guid TenantId { get; }
}

