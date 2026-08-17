using Api.Configuration;
using Api.Data;
using Api.Logging;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.MealPrep;

/// <summary>
///     The durable queue of pending recipe image renditions.
/// </summary>
public interface IRecipeImageDerivativeQueue
{
    /// <summary>Queues every rendition width for an image. Already-queued widths are left alone.</summary>
    Task EnqueueAllWidthsAsync(string sourceObjectKey, CancellationToken cancellationToken = default);

    /// <summary>Queues a single width, used when a read finds a rendition missing.</summary>
    Task EnqueueWidthAsync(string sourceObjectKey, int width, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Takes the next job that is due, marking it as claimed, or returns null when nothing is
    ///     waiting.
    /// </summary>
    Task<RecipeImageDerivativeJob?> ClaimNextAsync(CancellationToken cancellationToken = default);

    Task CompleteAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default);

    Task FailAsync(RecipeImageDerivativeJob job, Exception exception, CancellationToken cancellationToken = default);

    /// <summary>Drops any queued work for an image, for when the image itself is deleted.</summary>
    Task RemoveForImageAsync(string sourceObjectKey, CancellationToken cancellationToken = default);
}

/// <summary>
///     Postgres-backed implementation of <see cref="IRecipeImageDerivativeQueue" />. A table rather
///     than an in-memory channel so queued work survives a restart and a failure leaves a row an
///     operator can look at.
/// </summary>
/// <remarks>
///     Claiming is serialised in-process by <see cref="ClaimLock" />, which is what stops two of
///     this app's own workers taking the same row. Two app instances could still both claim one job;
///     that is harmless, because processing a job writes the same rendition bytes to the same key,
///     so the worst case is duplicated work rather than a wrong result.
/// </remarks>
public sealed class RecipeImageDerivativeQueue(
    ApiDbContext db,
    IOptions<RecipeImageDerivativeOptions> options,
    TimeProvider timeProvider,
    ILogger<RecipeImageDerivativeQueue> logger
) : IRecipeImageDerivativeQueue
{
    private static readonly SemaphoreSlim ClaimLock = new(1, 1);

    private RecipeImageDerivativeOptions Options => options.Value;

    public async Task EnqueueAllWidthsAsync(string sourceObjectKey, CancellationToken cancellationToken = default) {
        foreach (var width in RecipeImageVariants.Widths) {
            await EnqueueWidthAsync(sourceObjectKey, width, cancellationToken);
        }
    }

    public async Task EnqueueWidthAsync(
        string sourceObjectKey,
        int width,
        CancellationToken cancellationToken = default
    ) {
        var alreadyQueued = await db.RecipeImageDerivativeJobs
            .AnyAsync(
                job => job.SourceObjectKey == sourceObjectKey && job.TargetWidth == width,
                cancellationToken
            );

        // A row in any state counts as queued: a succeeded one means the rendition exists, and a
        // failed one has already exhausted its retries, so re-queuing it on every read would turn a
        // broken image into an endless resize loop.
        if (alreadyQueued) return;

        db.RecipeImageDerivativeJobs.Add(
            RecipeImageDerivativeJob.CreateNew(sourceObjectKey, width, timeProvider.GetUtcNow().UtcDateTime)
        );

        try {
            await db.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException exception) {
            // Another request queued the same width between the check and the insert; the unique
            // index caught it, which is exactly the outcome wanted.
            db.ChangeTracker.Clear();

            using var scope = logger.BeginPropertyScope(
                ("recipe.image.key", sourceObjectKey),
                ("recipe.image.width", width)
            );
            logger.LogDebug(exception, "Recipe image rendition was already queued");
        }
    }

    public async Task<RecipeImageDerivativeJob?> ClaimNextAsync(CancellationToken cancellationToken = default) {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staleClaimCutoff = now - TimeSpan.FromMinutes(Options.StaleClaimTimeoutMinutes);

        await ClaimLock.WaitAsync(cancellationToken);
        try {
            var job = await db.RecipeImageDerivativeJobs
                .Where(candidate =>
                    (candidate.Status == RecipeImageDerivativeJobStatus.Pending
                     && candidate.NextAttemptAt <= now)
                    || (candidate.Status == RecipeImageDerivativeJobStatus.Processing
                        && candidate.ClaimedAt != null
                        && candidate.ClaimedAt < staleClaimCutoff)
                )
                .OrderBy(candidate => candidate.NextAttemptAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (job is null) return null;

            job.MarkClaimed(now);
            await db.SaveChangesAsync(cancellationToken);
            return job;
        } finally {
            ClaimLock.Release();
        }
    }

    public async Task CompleteAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default) {
        job.MarkSucceeded(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        RecipeImageDerivativeJob job,
        Exception exception,
        CancellationToken cancellationToken = default
    ) {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var retryDelay = RetryDelayFor(job.Attempts);
        var willRetry = job.HasAttemptsRemaining(Options.MaxAttempts);

        job.MarkAttemptFailed(exception.Message, now, retryDelay, Options.MaxAttempts);
        await db.SaveChangesAsync(cancellationToken);

        using var scope = logger.BeginPropertyScope(
            ("recipe.image.key", job.SourceObjectKey),
            ("recipe.image.width", job.TargetWidth),
            ("recipe.image.attempts", job.Attempts)
        );

        if (willRetry) {
            logger.LogWarning(exception, "Recipe image rendition failed; retrying");
            return;
        }

        // Last attempt: log at error so a recipe left without its rendition shows up rather than
        // failing silently.
        logger.LogError(exception, "Recipe image rendition failed after every attempt; giving up");
    }

    public async Task RemoveForImageAsync(string sourceObjectKey, CancellationToken cancellationToken = default) {
        var jobs = await db.RecipeImageDerivativeJobs
            .Where(job => job.SourceObjectKey == sourceObjectKey)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0) return;

        db.RecipeImageDerivativeJobs.RemoveRange(jobs);
        await db.SaveChangesAsync(cancellationToken);
    }

    private TimeSpan RetryDelayFor(int attempts) {
        var exponent = Math.Min(Math.Max(attempts - 1, 0), 6);
        return TimeSpan.FromSeconds(Options.RetryBackoffSeconds * Math.Pow(2, exponent));
    }
}
