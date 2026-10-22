using AdNoir.Domain.Abstractions.Entities;

namespace AdNoir.Notification.IntegrationChecks;

internal sealed class BrokerProbeEntity : IEntity, IMultiTenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }
}
