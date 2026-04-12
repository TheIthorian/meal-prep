namespace Api.Configuration;

/// <summary>
///     Stores the configuration values required by <see cref="Services.S3StorageService" />.
/// </summary>
public class S3StorageConfiguration
{
    public string? ServiceUrl { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string BucketName { get; set; } = "myapp-dev";
    public string Region { get; set; } = "eu-west-1";
}
