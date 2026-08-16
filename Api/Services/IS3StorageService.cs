namespace Api.Services;

public interface IS3StorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>
    ///     Uploads to a caller-chosen key rather than generating one. Used where the key has to be
    ///     derivable from another object's key, such as the resized variants of a recipe image.
    /// </summary>
    Task UploadFileAtKeyAsync(Stream fileStream, string key, string contentType);

    Task<Stream> DownloadFileAsync(string s3Key);

    /// <summary>
    ///     Returns the object's contents, or null when no object exists at the key. Distinguishes a
    ///     missing object from a storage failure, which <see cref="DownloadFileAsync" /> cannot.
    /// </summary>
    Task<Stream?> TryDownloadFileAsync(string s3Key);

    Task DeleteFileAsync(string s3Key);
}
