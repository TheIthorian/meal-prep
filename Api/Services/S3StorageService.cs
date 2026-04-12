using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Api.Configuration;
using Api.Logging;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
///     Stores and retrieves files from S3-compatible storage.
/// </summary>
public class S3StorageService : IS3StorageService
{
    private readonly string _bucketName;
    private readonly ILogger<S3StorageService> _logger;
    private readonly IAmazonS3 _s3Client;

    public S3StorageService(IOptions<S3StorageConfiguration> configuration, ILogger<S3StorageService> logger) {
        _logger = logger;
        var s3Config = configuration.Value;
        var serviceUrl = s3Config.ServiceUrl;
        var accessKey = s3Config.AccessKey;
        var secretKey = s3Config.SecretKey;
        _bucketName = s3Config.BucketName;

        var config = new AmazonS3Config { AuthenticationRegion = s3Config.Region };

        if (!string.IsNullOrEmpty(serviceUrl)) {
            // MinIO or custom S3 compatible storage
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = true; // Required for MinIO
        } else {
            // Real AWS S3
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(config.AuthenticationRegion);
        }

        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType) {
        using var methodTiming = System.Diagnostics.Activity.Current.BeginAppMethodEvent();

        var key = $"{Guid.NewGuid()}_{fileName}";

        using var scope = _logger.BeginPropertyScope(
            ("s3.bucket", _bucketName),
            ("s3.key", key),
            ("file.contentType", contentType)
        );
        _logger.LogInformation("Uploading file to S3");

        var uploadRequest = new TransferUtilityUploadRequest {
            InputStream = fileStream, Key = key, BucketName = _bucketName, ContentType = contentType
        };

        var fileTransferUtility = new TransferUtility(_s3Client);
        await fileTransferUtility.UploadAsync(uploadRequest);

        return key;
    }

    public async Task<Stream> DownloadFileAsync(string s3Key) {
        using var methodTiming = System.Diagnostics.Activity.Current.BeginAppMethodEvent();

        using var scope = _logger.BeginPropertyScope(("s3.bucket", _bucketName), ("s3.key", s3Key));
        _logger.LogInformation("Downloading file from S3");

        var request = new GetObjectRequest { BucketName = _bucketName, Key = s3Key };

        var response = await _s3Client.GetObjectAsync(request);
        return response.ResponseStream;
    }

    public async Task DeleteFileAsync(string s3Key) {
        using var methodTiming = System.Diagnostics.Activity.Current.BeginAppMethodEvent();

        using var scope = _logger.BeginPropertyScope(("s3.bucket", _bucketName), ("s3.key", s3Key));
        _logger.LogInformation("Deleting file from S3");

        var request = new DeleteObjectRequest { BucketName = _bucketName, Key = s3Key };

        await _s3Client.DeleteObjectAsync(request);
    }
}
