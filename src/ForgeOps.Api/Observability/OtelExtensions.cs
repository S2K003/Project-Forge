using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ForgeOps.Api.Observability;

/// <summary>
/// OTLP export is opt-in via configuration (ProjectForge.md §7A — a free-tier trace/log
/// sink). With nothing configured the app still runs and emits to the console in dev.
/// </summary>
internal static class OtelExtensions
{
    public static TracerProviderBuilder AddOtlpExporterIfConfigured(
        this TracerProviderBuilder builder, IConfiguration configuration)
    {
        if (HasOtlpEndpoint(configuration))
        {
            builder.AddOtlpExporter();
        }

        return builder;
    }

    public static MeterProviderBuilder AddOtlpExporterIfConfigured(
        this MeterProviderBuilder builder, IConfiguration configuration)
    {
        if (HasOtlpEndpoint(configuration))
        {
            builder.AddOtlpExporter();
        }

        return builder;
    }

    private static bool HasOtlpEndpoint(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
}
