namespace Api.Services;

public interface IS3StorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<Stream> DownloadFileAsync(string s3Key);
    Task DeleteFileAsync(string s3Key);
}
