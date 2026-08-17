using System.Threading.Channels;

namespace Api.Services.MealPrep;

/// <summary>
///     Hands collection import job ids from the request that created them to the background worker that
///     runs them. The queue is only a wake-up signal — the job's own state lives in the database, so a
///     dropped signal costs a delayed start, never lost progress.
/// </summary>
public class RecipeCollectionImportJobQueue
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
    );

    public void Enqueue(Guid jobId) {
        channel.Writer.TryWrite(jobId);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) {
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}
