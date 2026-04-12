namespace Api.Services;

/// <summary>
///     Hangfire queue names used by background workers in this application.
/// </summary>
public static class BackgroundJobQueues
{
    public const string Cron = "cron";
    public const string Default = "default";
    public const string Cleanup = "cleanup";
    public const string RecurringPaymentSync = "recurring-payment-sync";
}
