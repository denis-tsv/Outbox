using System.Diagnostics;

namespace Outbox.WebApi.Telemetry;

public static class ActivitySources
{
    public const string OutboxSource = "Outbox";
    public static readonly ActivitySource Tracing = new(OutboxSource);
}