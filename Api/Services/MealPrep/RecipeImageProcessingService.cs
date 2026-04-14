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

    public async Task<ProcessedRecipeImagePayload> OptimizeForWebAsync(
        Stream sourceStream,
        string originalFileName,
        CancellationToken cancellationToken = default
    ) {
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);
        var resize = BuildResizeOptions(image.Width, image.Height);
        if (resize is not null) {
            image.Mutate(context => context.Resize(resize));
        }

        await using var optimizedStream = new MemoryStream();
        await image.SaveAsync(optimizedStream, WebpEncoder, cancellationToken);

        return new ProcessedRecipeImagePayload(
            optimizedStream.ToArray(),
            RecipeImageUploadConstants.OptimizedContentType,
            BuildOptimizedFileName(originalFileName),
            image.Width,
            image.Height
        );
    }

    private static ResizeOptions? BuildResizeOptions(int width, int height) {
        var max = RecipeImageUploadConstants.MaxPixelDimension;
        if (width <= max && height <= max) {
            return null;
        }

        return new ResizeOptions {
            Size = new Size(max, max),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3,
        };
    }

    private static string BuildOptimizedFileName(string originalFileName) {
        var safeFileName = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(safeFileName)) {
            safeFileName = "image";
        }

        return $"{safeFileName}{RecipeImageUploadConstants.OptimizedExtension}";
    }
}

public sealed record ProcessedRecipeImagePayload(
    byte[] Data,
    string ContentType,
    string FileName,
    int Width,
    int Height
);
