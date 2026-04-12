namespace Api.Configuration;

/// <summary>
///     Defines the OpenTelemetry exporter settings used during application startup.
/// </summary>
public class OpenTelemetryOptions
{
    public bool DisableForTests { get; set; }
    public bool Enabled { get; set; }
    public bool EnableConsoleExporter { get; set; }
    public string? ExporterEndpoint { get; set; }
    public string? LogsExporterEndpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? AxiomDataset { get; set; }

    public string? EffectiveLogsExporterEndpoint => LogsExporterEndpoint ?? ExporterEndpoint;
}
