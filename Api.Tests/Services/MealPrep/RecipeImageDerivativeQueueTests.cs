using Api.Configuration;
using Api.Data;
using Api.Models;
using Api.Services.MealPrep;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Services.MealPrep;

public class RecipeImageDerivativeQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EnqueueAllWidthsAsync_ShouldQueueOneJobPerRenditionWidth() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out _);

        await queue.EnqueueAllWidthsAsync("photo.png");

        var jobs = await db.RecipeImageDerivativeJobs.ToListAsync();
        Assert.Equal(RecipeImageVariants.Widths.Count, jobs.Count);
        Assert.All(RecipeImageVariants.Widths, width => Assert.Contains(jobs, job => job.TargetWidth == width));
        Assert.All(jobs, job => Assert.Equal(RecipeImageDerivativeJobStatus.Pending, job.Status));
    }

    [Fact]
    public async Task EnqueueWidthAsync_ShouldNotQueueTheSameRenditionTwice() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out _);

        await queue.EnqueueWidthAsync("photo.png", 400);
        await queue.EnqueueWidthAsync("photo.png", 400);

        Assert.Equal(1, await db.RecipeImageDerivativeJobs.CountAsync());
    }

    [Fact]
    public async Task ClaimNextAsync_ShouldTakeADueJobAndMarkItProcessing() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out _);
        await queue.EnqueueWidthAsync("photo.png", 400);

        var job = await queue.ClaimNextAsync();

        Assert.NotNull(job);
        Assert.Equal(RecipeImageDerivativeJobStatus.Processing, job.Status);
        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public async Task ClaimNextAsync_ShouldNotHandTheSameJobToTwoWorkers() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out _);
        await queue.EnqueueWidthAsync("photo.png", 400);

        var first = await queue.ClaimNextAsync();
        var second = await queue.ClaimNextAsync();

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task ClaimNextAsync_ShouldReturnNullWhenTheQueueIsEmpty() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out _);

        Assert.Null(await queue.ClaimNextAsync());
    }

    [Fact]
    public async Task FailAsync_ShouldScheduleARetryWhileAttemptsRemain() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out var clock);
        await queue.EnqueueWidthAsync("photo.png", 400);
        var job = await queue.ClaimNextAsync();

        await queue.FailAsync(job!, new InvalidOperationException("decode failed"));

        Assert.Equal(RecipeImageDerivativeJobStatus.Pending, job!.Status);
        Assert.Equal("decode failed", job.LastError);
        Assert.True(job.NextAttemptAt > clock.Now.UtcDateTime);
        Assert.Null(await queue.ClaimNextAsync());
    }

    [Fact]
    public async Task ClaimNextAsync_ShouldPickUpARetryOnceItsBackoffHasElapsed() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out var clock);
        await queue.EnqueueWidthAsync("photo.png", 400);
        var job = await queue.ClaimNextAsync();
        await queue.FailAsync(job!, new InvalidOperationException("decode failed"));

        clock.Advance(TimeSpan.FromHours(1));

        var retried = await queue.ClaimNextAsync();

        Assert.NotNull(retried);
        Assert.Equal(2, retried.Attempts);
    }

    [Fact]
    public async Task FailAsync_ShouldGiveUpAfterTheAttemptLimitAndKeepTheError() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out var clock, options => options.MaxAttempts = 2);
        await queue.EnqueueWidthAsync("photo.png", 400);

        for (var attempt = 0; attempt < 2; attempt++) {
            var job = await queue.ClaimNextAsync();
            Assert.NotNull(job);
            await queue.FailAsync(job, new InvalidOperationException("decode failed"));
            clock.Advance(TimeSpan.FromHours(1));
        }

        var failed = await db.RecipeImageDerivativeJobs.SingleAsync();
        Assert.Equal(RecipeImageDerivativeJobStatus.Failed, failed.Status);
        Assert.Equal("decode failed", failed.LastError);
        Assert.Null(await queue.ClaimNextAsync());
    }

    /// <summary>
    ///     A job claimed by a process that then died must not stay stuck: that is what makes queued
    ///     work survive a restart.
    /// </summary>
    [Fact]
    public async Task ClaimNextAsync_ShouldReclaimAJobAbandonedByAStoppedWorker() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out var clock, options => options.StaleClaimTimeoutMinutes = 10);
        await queue.EnqueueWidthAsync("photo.png", 400);
        await queue.ClaimNextAsync();

        clock.Advance(TimeSpan.FromMinutes(11));

        var reclaimed = await queue.ClaimNextAsync();

        Assert.NotNull(reclaimed);
        Assert.Equal(2, reclaimed.Attempts);
    }

    [Fact]
    public async Task CompleteAsync_ShouldTakeTheJobOutOfTheQueue() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out var clock);
        await queue.EnqueueWidthAsync("photo.png", 400);
        var job = await queue.ClaimNextAsync();

        await queue.CompleteAsync(job!);
        clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(RecipeImageDerivativeJobStatus.Succeeded, job!.Status);
        Assert.Null(await queue.ClaimNextAsync());
    }

    [Fact]
    public async Task EnqueueWidthAsync_ShouldNotRequeueARenditionThatAlreadyFailed() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out var clock, options => options.MaxAttempts = 1);
        await queue.EnqueueWidthAsync("photo.png", 400);
        var job = await queue.ClaimNextAsync();
        await queue.FailAsync(job!, new InvalidOperationException("decode failed"));
        clock.Advance(TimeSpan.FromHours(1));

        await queue.EnqueueWidthAsync("photo.png", 400);

        Assert.Equal(1, await db.RecipeImageDerivativeJobs.CountAsync());
        Assert.Null(await queue.ClaimNextAsync());
    }

    [Fact]
    public async Task RemoveForImageAsync_ShouldDropEveryQueuedRenditionForTheImage() {
        await using var db = RecipeImageTestFactory.CreateInMemoryDatabase();
        var queue = BuildQueue(db, out _);
        await queue.EnqueueAllWidthsAsync("photo.png");
        await queue.EnqueueAllWidthsAsync("other.png");

        await queue.RemoveForImageAsync("photo.png");

        var remaining = await db.RecipeImageDerivativeJobs.ToListAsync();
        Assert.All(remaining, job => Assert.Equal("other.png", job.SourceObjectKey));
        Assert.Equal(RecipeImageVariants.Widths.Count, remaining.Count);
    }

    private static RecipeImageDerivativeQueue BuildQueue(
        ApiDbContext db,
        out TestTimeProvider clock,
        Action<RecipeImageDerivativeOptions>? configure = null
    ) {
        var options = new RecipeImageDerivativeOptions();
        configure?.Invoke(options);
        clock = new TestTimeProvider(Now);

        return new RecipeImageDerivativeQueue(
            db,
            Options.Create(options),
            clock,
            NullLogger<RecipeImageDerivativeQueue>.Instance
        );
    }
}
