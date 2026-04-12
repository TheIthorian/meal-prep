using Api.Configuration;
using Api.Logging;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
///     Hangfire entrypoint for the nightly retention cleanup job.
/// </summary>
public class CleanupBackgroundJobs(
    IOptions<AppRolesOptions> appRoles,
    RetentionCleanupService retentionCleanupService,
    ILogger<CleanupBackgroundJobs> logger
)
{
    public const string NightlyCleanupJobId = "nightly-retention-cleanup";

    [AutomaticRetry(Attempts = 3)]
    [Queue(BackgroundJobQueues.Cleanup)]
    public async Task RunNightlyCleanup(CancellationToken cancellationToken) {
        using var executionContext = AppExecutionContext.Push(null, Guid.NewGuid().ToString("N"));
        using var methodTiming = ActivityMethodTelemetryExtensions.BeginRootAppMethodEvent();
        using var traceScope = logger.BeginTraceScope();

        if (!appRoles.Value.HasRole(AppRoles.WorkerCleanup)) {
            logger.LogDebug(
                "Skipping nightly cleanup because app role {Role} is not enabled on this worker",
                AppRoles.WorkerCleanup
            );
            return;
        }

        await retentionCleanupService.RunAsync(cancellationToken);
    }
}
