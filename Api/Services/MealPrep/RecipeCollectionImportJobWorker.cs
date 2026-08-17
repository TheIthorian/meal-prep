using Microsoft.Extensions.Options;

namespace Api.Services.MealPrep;

/// <summary>
///     Options controlling how queued collection imports are executed.
/// </summary>
public class RecipeCollectionImportJobOptions
{
    public const string SectionName = "RecipeCollectionImport";

    /// <summary>
    ///     When false the background worker stays idle and jobs must be run explicitly. Tests use this so
    ///     they can drive a job deterministically.
    /// </summary>
    public bool ProcessInBackground { get; set; } = true;

    /// <summary>
    ///     Whether jobs left mid-flight by a process restart are re-queued on startup.
    /// </summary>
    public bool RecoverInterruptedJobsOnStartup { get; set; } = true;
}

/// <summary>
///     Drains the collection import queue, running each job in its own service scope so a long import
///     does not hold the originating HTTP request open.
/// </summary>
public class RecipeCollectionImportJobWorker(
    RecipeCollectionImportJobQueue queue,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<RecipeCollectionImportJobOptions> options,
    ILogger<RecipeCollectionImportJobWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!options.Value.ProcessInBackground) {
            logger.LogInformation("Recipe collection import background processing is disabled");
            return;
        }

        if (options.Value.RecoverInterruptedJobsOnStartup) await RecoverInterruptedJobsAsync(stoppingToken);

        await foreach (var jobId in queue.ReadAllAsync(stoppingToken)) {
            if (stoppingToken.IsCancellationRequested) return;

            await RunJobAsync(jobId, stoppingToken);
        }
    }

    private async Task RecoverInterruptedJobsAsync(CancellationToken cancellationToken) {
        try {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<RecipeCollectionImportJobService>();

            var jobIds = await service.RecoverInterruptedJobsAsync(cancellationToken);
            foreach (var jobId in jobIds) queue.Enqueue(jobId);

            if (jobIds.Length > 0)
                logger.LogInformation("Re-queued {JobCount} interrupted collection import jobs", jobIds.Length);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            logger.LogError(exception, "Failed to recover interrupted collection import jobs");
        }
    }

    private async Task RunJobAsync(Guid jobId, CancellationToken cancellationToken) {
        try {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<RecipeCollectionImportJobService>();

            await service.RunAsync(jobId, cancellationToken);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            logger.LogError(exception, "Unhandled failure running collection import job {ImportJobId}", jobId);
        }
    }
}
