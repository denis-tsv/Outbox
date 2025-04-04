using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Outbox.WebApi.Telemetry;

public static class TelemetryExtensions
{
    public static Dictionary<string, string> GetHeaders(this ActivityContext? context)
    {
        Dictionary<string, string> result = new();

        if (context == null) return result;

        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(context.Value, Baggage.Current),
            result,
            (dictionary, key, value) => dictionary[key] = value);

        return result;
    }
}