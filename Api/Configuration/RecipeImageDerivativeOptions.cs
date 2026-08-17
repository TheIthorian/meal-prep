namespace Api.Configuration;

/// <summary>
///     Tuning for the background generation of recipe image renditions.
/// </summary>
public class RecipeImageDerivativeOptions
{
    /// <summary>
    ///     How many renditions may be generated at once. Deliberately small: resizing is CPU bound,
    ///     and the point of moving it off the request path is that a deep queue must never take the
    ///     cores request handling needs, however much work is waiting.
    /// </summary>
    public int WorkerCount { get; set; } = 2;

    /// <summary>How long a worker waits before polling again once the queue is empty.</summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>Attempts a single job gets before it is left in the failed state.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base delay before a failed job is retried; doubled per attempt.</summary>
    public int RetryBackoffSeconds { get; set; } = 30;

    /// <summary>
    ///     How long a claimed job may sit before another worker may take it. This is what makes a
    ///     job interrupted by a restart get picked up again rather than staying stuck.
    /// </summary>
    public int StaleClaimTimeoutMinutes { get; set; } = 10;
}
