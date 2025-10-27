using System.Diagnostics;

namespace Pipelane.Application.Common;

/// <summary>
/// Expose les sources d'activités OpenTelemetry partagées.
/// </summary>
public static class TelemetrySources
{
    public static readonly ActivitySource Messaging = new("Pipelane.Messaging");
    public static readonly ActivitySource Webhooks = new("Pipelane.Webhooks");
    public static readonly ActivitySource Followups = new("Pipelane.Followups");
}
