namespace Api.Configuration;

/// <summary>
///     Configuration for the nightly retention cleanup worker.
/// </summary>
public class RetentionCleanupOptions
{
    public const int DefaultRetentionDays = 7;

    /// <summary>
    ///     Number of days to keep uploads and empty workspaces before they are removed.
    /// </summary>
    public int RetentionDays { get; set; } = DefaultRetentionDays;
}
