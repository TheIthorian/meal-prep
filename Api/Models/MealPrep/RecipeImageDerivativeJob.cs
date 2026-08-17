using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Api.Models;

/// <summary>
///     The states a <see cref="RecipeImageDerivativeJob" /> moves through. Stored as text rather
///     than an enum ordinal so a row stays readable, and so adding a state later cannot renumber
///     the existing ones.
/// </summary>
public static class RecipeImageDerivativeJobStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

/// <summary>
///     One queued resize: "build the <see cref="TargetWidth" /> rendition of
///     <see cref="SourceObjectKey" />". Rows are the durable queue the background worker drains, so
///     a resize outlives the request that asked for it and survives a restart mid-flight.
/// </summary>
[Index(nameof(SourceObjectKey), nameof(TargetWidth), IsUnique = true)]
[Index(nameof(Status), nameof(NextAttemptAt))]
public class RecipeImageDerivativeJob : Entity
{
    /// <summary>
    ///     The object key of the image as uploaded, which is also what the recipe records.
    /// </summary>
    [MaxLength(512)]
    public string SourceObjectKey { get; private set; } = string.Empty;

    public int TargetWidth { get; private set; }

    [MaxLength(32)]
    public string Status { get; private set; } = RecipeImageDerivativeJobStatus.Pending;

    public int Attempts { get; private set; }

    /// <summary>
    ///     The earliest time a worker may pick this job up. Retries push it into the future so a
    ///     failing job does not spin.
    /// </summary>
    public DateTime NextAttemptAt { get; private set; }

    /// <summary>
    ///     When the current attempt was claimed. A claim older than the configured timeout is
    ///     treated as abandoned — the process holding it died — and may be reclaimed.
    /// </summary>
    public DateTime? ClaimedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    [MaxLength(2048)]
    public string? LastError { get; private set; }

    public static RecipeImageDerivativeJob CreateNew(string sourceObjectKey, int targetWidth, DateTime now) {
        return new RecipeImageDerivativeJob {
            SourceObjectKey = sourceObjectKey,
            TargetWidth = targetWidth,
            Status = RecipeImageDerivativeJobStatus.Pending,
            NextAttemptAt = now,
        };
    }

    public void MarkClaimed(DateTime now) {
        Status = RecipeImageDerivativeJobStatus.Processing;
        ClaimedAt = now;
        Attempts += 1;
    }

    public void MarkSucceeded(DateTime now) {
        Status = RecipeImageDerivativeJobStatus.Succeeded;
        ClaimedAt = null;
        CompletedAt = now;
        LastError = null;
    }

    /// <summary>
    ///     Records a failed attempt. The job goes back to pending with a delay until it has been
    ///     tried <paramref name="maxAttempts" /> times, after which it is left failed for an
    ///     operator to see rather than retried forever.
    /// </summary>
    public void MarkAttemptFailed(string error, DateTime now, TimeSpan retryDelay, int maxAttempts) {
        ClaimedAt = null;
        LastError = Truncate(error, 2048);

        if (Attempts >= maxAttempts) {
            Status = RecipeImageDerivativeJobStatus.Failed;
            CompletedAt = now;
            return;
        }

        Status = RecipeImageDerivativeJobStatus.Pending;
        NextAttemptAt = now + retryDelay;
    }

    public bool HasAttemptsRemaining(int maxAttempts) {
        return Attempts < maxAttempts;
    }

    private static string Truncate(string value, int maxLength) {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
