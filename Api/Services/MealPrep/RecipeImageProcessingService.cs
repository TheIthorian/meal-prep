using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Api.Services.MealPrep;

/// <summary>
///     Resizes and re-encodes recipe images for efficient web delivery.
/// </summary>
public sealed class RecipeImageProcessingService
{
    private static readonly WebpEncoder WebpEncoder = new() {
        Quality = 82,
        Method = WebpEncodingMethod.BestQuality,
        FileFormat = WebpFileFormatType.Lossy,
    };

    /// <summary>
    ///     Re-encodes a stored original at a narrower width, for serving to layouts that display it
    ///     far smaller than it was uploaded. Images narrower than the target are re-encoded at their
    ///     own size rather than upscaled.
    /// </summary>
    /// <remarks>
    ///     Only ever called from the background worker: this is the CPU-bound part of handling a
    ///     recipe image, and running it on a request thread is what made a batch import slow the
    ///     whole server down.
    /// </remarks>
    public async Task<byte[]> ResizeToWidthAsync(
        Stream sourceStream,
        int targetWidth,
        CancellationToken cancellationToken = default
    ) {
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);

        // Height is capped as well as width so a very tall image cannot come out enormous at a
        // narrow width, which is the cap the upload path used to apply before storing.
        var maxHeight = RecipeImageUploadConstants.MaxPixelDimension;
        if (image.Width > targetWidth || image.Height > maxHeight) {
            image.Mutate(context => context.Resize(new ResizeOptions {
                Size = new Size(targetWidth, maxHeight),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3,
            }));
        }

        await using var resizedStream = new MemoryStream();
        await image.SaveAsync(resizedStream, WebpEncoder, cancellationToken);
        return resizedStream.ToArray();
    }
}
