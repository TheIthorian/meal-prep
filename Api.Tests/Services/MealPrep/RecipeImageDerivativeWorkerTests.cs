using Api.Configuration;
using Api.Models;
using Api.Services.MealPrep;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Services.MealPrep;

public class RecipeImageDerivativeWorkerTests
{
    /// <summary>
    ///     The whole point of the worker: however deep the queue is, only the configured number of
    ///     resizes run at once, so image processing cannot take the cores request handling needs.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExecuteAsync_ShouldNeverRunMoreResizesAtOnceThanConfigured(int workerCount) {
        var queue = new StubQueue(jobCount: 60);
        var processor = new ConcurrencyTrackingProcessor(TimeSpan.FromMilliseconds(5));
        using var worker = BuildWorker(queue, processor, options => options.WorkerCount = workerCount);

        await worker.StartAsync(CancellationToken.None);
        await queue.Drained.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(
            processor.MaxConcurrency <= workerCount,
            $"observed {processor.MaxConcurrency} concurrent resizes with a bound of {workerCount}"
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessAndCompleteQueuedJobs() {
        var queue = new StubQueue(jobCount: 5);
        var processor = new ConcurrencyTrackingProcessor(TimeSpan.Zero);
        using var worker = BuildWorker(queue, processor, options => options.WorkerCount = 2);

        await worker.StartAsync(CancellationToken.None);
        await queue.Drained.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(5, processor.ProcessedCount);
        Assert.Equal(5, queue.CompletedCount);
        Assert.Equal(0, queue.FailedCount);
    }

    /// <summary>
    ///     A resize that throws must be handed back to the queue so it is retried and logged, not
    ///     swallowed leaving the recipe without an image.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldReportAFailedResizeToTheQueue() {
        var queue = new StubQueue(jobCount: 3);
        var processor = new ThrowingProcessor();
        using var worker = BuildWorker(queue, processor, options => options.WorkerCount = 1);

        await worker.StartAsync(CancellationToken.None);
        await queue.Drained.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, queue.FailedCount);
        Assert.Equal(0, queue.CompletedCount);
    }

    private static RecipeImageDerivativeWorker BuildWorker(
        IRecipeImageDerivativeQueue queue,
        IRecipeImageDerivativeProcessor processor,
        Action<RecipeImageDerivativeOptions> configure
    ) {
        var options = new RecipeImageDerivativeOptions { PollIntervalSeconds = 1 };
        configure(options);

        var services = new ServiceCollection();
        services.AddSingleton(queue);
        services.AddSingleton(processor);

        return new RecipeImageDerivativeWorker(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<RecipeImageDerivativeWorker>.Instance
        );
    }

    private sealed class StubQueue(int jobCount) : IRecipeImageDerivativeQueue
    {
        private int handedOut;
        private int settled;

        public TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CompletedCount { get; private set; }

        public int FailedCount { get; private set; }

        public Task<RecipeImageDerivativeJob?> ClaimNextAsync(CancellationToken cancellationToken = default) {
            var next = Interlocked.Increment(ref handedOut);
            if (next > jobCount) return Task.FromResult<RecipeImageDerivativeJob?>(null);

            return Task.FromResult<RecipeImageDerivativeJob?>(
                RecipeImageDerivativeJob.CreateNew($"photo-{next}.png", 400, DateTime.UtcNow)
            );
        }

        public Task CompleteAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default) {
            lock (this) CompletedCount++;
            Settle();
            return Task.CompletedTask;
        }

        public Task FailAsync(
            RecipeImageDerivativeJob job,
            Exception exception,
            CancellationToken cancellationToken = default
        ) {
            lock (this) FailedCount++;
            Settle();
            return Task.CompletedTask;
        }

        public Task EnqueueAllWidthsAsync(string sourceObjectKey, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task EnqueueWidthAsync(
            string sourceObjectKey,
            int width,
            CancellationToken cancellationToken = default
        ) {
            return Task.CompletedTask;
        }

        public Task RemoveForImageAsync(string sourceObjectKey, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        private void Settle() {
            if (Interlocked.Increment(ref settled) >= jobCount) Drained.TrySetResult();
        }
    }

    private sealed class ConcurrencyTrackingProcessor(TimeSpan duration) : IRecipeImageDerivativeProcessor
    {
        private int inFlight;

        public int MaxConcurrency { get; private set; }

        public int ProcessedCount { get; private set; }

        public async Task ProcessAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default) {
            var current = Interlocked.Increment(ref inFlight);
            lock (this) {
                MaxConcurrency = Math.Max(MaxConcurrency, current);
                ProcessedCount++;
            }

            try {
                if (duration > TimeSpan.Zero) await Task.Delay(duration, cancellationToken);
            } finally {
                Interlocked.Decrement(ref inFlight);
            }
        }
    }

    private sealed class ThrowingProcessor : IRecipeImageDerivativeProcessor
    {
        public Task ProcessAsync(RecipeImageDerivativeJob job, CancellationToken cancellationToken = default) {
            throw new InvalidOperationException("decode failed");
        }
    }
}
