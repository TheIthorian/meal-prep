using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Api.Services.MealPrep;

/// <summary>
///     Resizes and re-encodes recipe images for efficient web delivery.
/// </summary>
public sealed class RecipeImageProcessingService
{
    /// <summary>
    ///     <see cref="WebpEncodingMethod" /> trades encode time against file size, and the curve is
    ///     not smooth. Measured on a 1500x1125 JPEG with ImageSharp 3.1.12, encoding the full-size
    ///     WebP:
    ///     <code>
    ///     Level6 (BestQuality)  789 ms   132,740 bytes
    ///     Level5                791 ms   132,740 bytes
    ///     Level4                765 ms   135,638 bytes
    ///     Level3                566 ms   138,584 bytes
    ///     Level2                114 ms   141,348 bytes
    ///     Level1                 82 ms   157,346 bytes
    ///     Level0                 71 ms   167,368 bytes
    ///     </code>
    ///     The cliff sits between Level2 and Level3: Level2 encodes 7x cheaper than the BestQuality
    ///     this used to run at, for 6.5% more bytes. Below Level2 the bytes start climbing sharply
    ///     for very little further time saved, so Level2 is the knee of the curve.
    /// </summary>
    private static readonly WebpEncoder WebpEncoder = new() {
        Quality = 82,
        Method = WebpEncodingMethod.Level2,
        FileFormat = WebpFileFormatType.Lossy,
    };

    /// <summary>
    ///     Decodes the source once and encodes the full-size image together with a rendition at each
    ///     requested width, all from that single decode.
    /// </summary>
    /// <remarks>
    ///     Renditions used to be produced by handing the encoded full-size WebP back in as a stream
    ///     once per width, which decoded it again every time — three decodes for one upload. Here the
    ///     source is decoded once, cloned per width, and every clone is encoded in parallel.
    ///     This holds the decoded image, one clone per width and every encoded byte[] in memory at
    ///     the same time. That is bounded and small at
    ///     <see cref="RecipeImageUploadConstants.MaxPixelDimension" /> (1600) — roughly 10 MB of
    ///     pixels plus a few hundred KB of output — but it is the cost of the parallelism, and it
    ///     would need revisiting if the dimension cap grew substantially.
    /// </remarks>
    public async Task<ProcessedRecipeImageSet> OptimizeForWebAsync(
        Stream sourceStream,
        string originalFileName,
        IReadOnlyList<int> renditionWidths,
        CancellationToken cancellationToken = default
    ) {
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);

        var resize = BuildResizeOptions(image.Width, image.Height);
        if (resize is not null) {
            image.Mutate(context => context.Resize(resize));
        }

        // Clone before any encoding starts: an Image is not thread-safe, so nothing may read the
        // decoded image while the tasks below are running against it.
        var clones = renditionWidths.Select(width => (Width: width, Image: CloneAtWidth(image, width))).ToList();

        try {
            var fullSizeTask = EncodeAsync(image, cancellationToken);
            var renditionTasks = clones
                .Select(async clone => new RecipeImageRendition(clone.Width, await EncodeAsync(clone.Image, cancellationToken)))
                .ToList();

            var fullSizeData = await fullSizeTask;
            var renditions = await Task.WhenAll(renditionTasks);

            return new ProcessedRecipeImageSet(
                new ProcessedRecipeImagePayload(
                    fullSizeData,
                    RecipeImageUploadConstants.OptimizedContentType,
                    BuildOptimizedFileName(originalFileName),
                    image.Width,
                    image.Height
                ),
                renditions
            );
        } finally {
            foreach (var clone in clones) {
                clone.Image.Dispose();
            }
        }
    }

    /// <summary>
    ///     Re-encodes an already-optimized image at a narrower width, for serving to layouts that
    ///     display it far smaller than it was stored. Images narrower than the target are returned
    ///     unchanged rather than upscaled.
    /// </summary>
    /// <remarks>
    ///     Still used by the read path, which backfills a missing rendition from the stored full-size
    ///     object and so genuinely has only encoded bytes to work from.
    /// </remarks>
    public async Task<byte[]> ResizeToWidthAsync(
        Stream sourceStream,
        int targetWidth,
        CancellationToken cancellationToken = default
    ) {
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);

        if (image.Width > targetWidth) {
            image.Mutate(context => context.Resize(BuildWidthResizeOptions(targetWidth)));
        }

        return await EncodeAsync(image, cancellationToken);
    }

    /// <summary>
    ///     Encoding is CPU-bound and synchronous inside ImageSharp, so it is pushed onto the pool to
    ///     let the callers above run several encodes at once.
    /// </summary>
    private static Task<byte[]> EncodeAsync(Image image, CancellationToken cancellationToken) {
        return Task.Run(
            () => {
                using var stream = new MemoryStream();
                image.Save(stream, WebpEncoder);
                return stream.ToArray();
            },
            cancellationToken
        );
    }

    /// <summary>
    ///     A copy of the image at <paramref name="targetWidth" />, or an unresized copy when it is
    ///     already that narrow — renditions never upscale.
    /// </summary>
    private static Image CloneAtWidth(Image image, int targetWidth) {
        return image.Width > targetWidth
            ? image.Clone(context => context.Resize(BuildWidthResizeOptions(targetWidth)))
            : image.Clone(_ => { });
    }

    private static ResizeOptions BuildWidthResizeOptions(int targetWidth) {
        return new ResizeOptions {
            Size = new Size(targetWidth, 0),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3,
        };
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

/// <summary>
///     One rendition's encoded bytes, tagged with the width it was built for so the caller can
///     derive its object key.
/// </summary>
public sealed record RecipeImageRendition(int Width, byte[] Data);

/// <summary>
///     Everything produced from a single decode of an uploaded image: the full-size object a recipe
///     records, plus a rendition per requested width.
/// </summary>
public sealed record ProcessedRecipeImageSet(
    ProcessedRecipeImagePayload FullSize,
    IReadOnlyList<RecipeImageRendition> Renditions
);
