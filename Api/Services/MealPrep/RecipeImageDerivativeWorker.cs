using Api.Configuration;
using Api.Logging;
using Api.Telemetry;
using Microsoft.Extensions.Options;

namespace Api.Services.MealPrep;

/// <summary>
///     Drains the recipe image rendition queue in the background.
/// </summary>
/// <remarks>
///     Concurrency is bounded by the number of loops started here, not by how much work is waiting:
///     a thousand queued renditions occupy exactly <see cref="RecipeImageDerivativeOptions.WorkerCount" />
///     threads, so an import can never take the cores that requests need. Each job runs in its own
///     DI scope so it gets a fresh <c>ApiDbContext</c> rather than sharing one for the process
///     lifetime.
/// </remarks>
public sealed class RecipeImageDerivativeWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<RecipeImageDerivativeOptions> options,
    ILogger<RecipeImageDerivativeWorker> logger
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) {
        var workerCount = Math.Max(1, options.Value.WorkerCount);

        logger.LogInformation(
            "Starting {WorkerCount} recipe image rendition worker(s)",
            workerCount
        );

        var loops = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(() => RunLoopAsync(stoppingToken), stoppingToken))
            .ToArray();

        return Task.WhenAll(loops);
    }

    private async Task RunLoopAsync(CancellationToken stoppingToken) {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested) {
            bool processedSomething;
            try {
                processedSomething = await TryProcessOneAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception exception) {
                // Reaching here means claiming or bookkeeping failed, not the resize itself (that is
                // handled per job). Back off rather than spinning against a database that is down.
                logger.LogError(exception, "The recipe image rendition worker loop failed");
                processedSomething = false;
            }

            if (processedSomething) continue;

            try {
                await Task.Delay(pollInterval, stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    /// <summary>
    ///     Claims and runs a single job. Returns false when the queue is empty, which is the signal
    ///     for the loop to wait before asking again.
    /// </summary>
    private async Task<bool> TryProcessOneAsync(CancellationToken stoppingToken) {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        var queue = scope.ServiceProvider.GetRequiredService<IRecipeImageDerivativeQueue>();
        var job = await queue.ClaimNextAsync(stoppingToken);
        if (job is null) return false;

        using var methodTiming = ActivityMethodTelemetryExtensions.BeginRootAppMethodEvent();

        var processor = scope.ServiceProvider.GetRequiredService<IRecipeImageDerivativeProcessor>();
        try {
            await processor.ProcessAsync(job, stoppingToken);
            await queue.CompleteAsync(job, stoppingToken);
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // Shutting down mid-job: leave the claim in place. It ages out as a stale claim and is
            // picked up again after the restart.
            using var cancellationScope = logger.BeginPropertyScope(
                ("recipe.image.key", job.SourceObjectKey),
                ("recipe.image.width", job.TargetWidth)
            );
            logger.LogInformation("Abandoning a recipe image rendition because the worker is stopping");
            throw;
        } catch (Exception exception) {
            await queue.FailAsync(job, exception, CancellationToken.None);
        }

        return true;
    }
}
