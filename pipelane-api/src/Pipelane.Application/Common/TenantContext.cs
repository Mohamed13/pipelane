namespace Pipelane.Application.Common;

public interface ITenantContext
{
    Guid TenantId { get; }
}

public sealed class TenantContext : ITenantContext
{
    public TenantContext(Guid tenantId) => TenantId = tenantId;
    /// <inheritdoc/>
    public Guid TenantId { get; }
}

