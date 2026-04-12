using Api.Configuration;
using Microsoft.OpenApi.Models;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Startup;

/// <summary>
///     Registers observability services and telemetry plumbing.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    private static AppLoggingOptions BuildAppLoggingOptions(IConfiguration configuration) {
        var options = new AppLoggingOptions();
        configuration.GetSection("AppLogging").Bind(options);
        return options;
    }

    private static OpenTelemetryOptions BuildOpenTelemetryOptions(IConfiguration configuration) {
        return new OpenTelemetryOptions {
            DisableForTests = string.Equals(
                configuration["Test:DisableOpenTelemetry"],
                "true",
                StringComparison.OrdinalIgnoreCase
            ),
            Enabled = string.Equals(
                configuration["OTEL_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase
            ),
            EnableConsoleExporter = string.Equals(
                configuration["OTEL_ENABLE_CONSOLE_EXPORTER"],
                "true",
                StringComparison.OrdinalIgnoreCase
            ),
            ExporterEndpoint = configuration["OTEL_EXPORTER_ENDPOINT"],
            LogsExporterEndpoint = configuration["OTEL_EXPORTER_LOGS_ENDPOINT"],
            ApiKey = configuration["OTEL_EXPORTER_API_KEY"],
            AxiomDataset = configuration["OTEL_AXIOM_DATASET"]
        };
    }

    private static string GetOtlpHeaders(string? apiKey, string? dataset) {
        var headers = new List<string>();
        if (!string.IsNullOrEmpty(apiKey)) headers.Add($"Authorization=Bearer {apiKey}");
        if (!string.IsNullOrEmpty(dataset)) headers.Add($"X-Axiom-Dataset={dataset}");
        return string.Join(",", headers);
    }

    extension(WebApplicationBuilder builder)
    {
        public void AddAppLogging() {
            var appLoggingOptions = BuildAppLoggingOptions(builder.Configuration);
            var frameworkLogLevel = appLoggingOptions.VerboseFrameworkInfoLogs
                ? LogLevel.Information
                : LogLevel.Warning;

            builder.Logging.Configure(options => {
                    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId
                                                      | ActivityTrackingOptions.SpanId
                                                      | ActivityTrackingOptions.ParentId;
                }
            );
            builder.Services.AddHttpLogging();
            builder.Logging.AddFilter("Microsoft.AspNetCore", frameworkLogLevel);
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", frameworkLogLevel);

            if (!builder.Environment.IsDevelopment()) {
                builder.Logging.ClearProviders();
                builder.Logging.AddFilter("Microsoft.AspNetCore", frameworkLogLevel);
                builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", frameworkLogLevel);
                builder.Logging.AddJsonConsole();
                return;
            }

            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
        }

        public void AddAppOpenTelemetry() {
            var openTelemetryOptions = BuildOpenTelemetryOptions(builder.Configuration);
            if (openTelemetryOptions.DisableForTests || !openTelemetryOptions.Enabled)
                return;

            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService($"{builder.Environment.ApplicationName}:{builder.Environment.EnvironmentName}")
                    .AddAttributes(
                        new Dictionary<string, object> { ["axiom.dataset"] = openTelemetryOptions.AxiomDataset ?? "" }
                    )
                    .AddTelemetrySdk()
                    .AddEnvironmentVariableDetector()
                )
                .WithTracing(tracing => {
                        tracing.AddSource(ActivityMethodTelemetryExtensions.ActivitySourceName)
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddNpgsql();

                        if (!string.IsNullOrEmpty(openTelemetryOptions.ExporterEndpoint))
                            tracing.AddOtlpExporter(exporterOptions => {
                                    exporterOptions.Endpoint = new Uri(openTelemetryOptions.ExporterEndpoint);
                                    exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                                    exporterOptions.Headers = GetOtlpHeaders(
                                        openTelemetryOptions.ApiKey,
                                        openTelemetryOptions.AxiomDataset
                                    );
                                }
                            );
                        else if (openTelemetryOptions.EnableConsoleExporter)
                            tracing.AddConsoleExporter();
                    }
                )
                .WithMetrics(metrics => {
                        metrics.AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddRuntimeInstrumentation();

                        // Axiom does not support OTel metrics yet, so console export is explicit.
                        if (openTelemetryOptions.EnableConsoleExporter)
                            metrics.AddConsoleExporter();
                    }
                )
                .WithLogging(
                    logging => {
                        if (!string.IsNullOrEmpty(openTelemetryOptions.EffectiveLogsExporterEndpoint))
                            logging.AddOtlpExporter(exporterOptions => {
                                    exporterOptions.Endpoint = new Uri(
                                        openTelemetryOptions.EffectiveLogsExporterEndpoint
                                    );
                                    exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                                    exporterOptions.Headers = GetOtlpHeaders(
                                        openTelemetryOptions.ApiKey,
                                        openTelemetryOptions.AxiomDataset
                                    );
                                }
                            );
                        else if (openTelemetryOptions.EnableConsoleExporter)
                            logging.AddConsoleExporter();
                    },
                    options => {
                        options.IncludeFormattedMessage = true;
                        options.IncludeScopes = true;
                        options.ParseStateValues = true;
                    }
                );
        }
    }

    extension(IServiceCollection services)
    {
        public void AddAppSwagger() {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options => {
                    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
                    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Meal Prep API", Version = "v1" });
                }
            );
        }
    }
}
