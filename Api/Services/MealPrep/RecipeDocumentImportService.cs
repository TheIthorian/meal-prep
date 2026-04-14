using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Domain;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace Api.Services.MealPrep;

/// <summary>
///     Extracts recipe text from uploaded documents and images, using local extraction first and optional external OCR for images.
/// </summary>
public sealed class RecipeDocumentImportService(
    RecipeImportService recipeImportService,
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAIConfiguration> openAiOptions,
    ILogger<RecipeDocumentImportService> logger
)
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) {
        "application/pdf",
        "text/plain",
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp"
    };

    public async Task<RecipeImportPreview> ImportFromUploadAsync(
        IFormFile? file,
        Guid workspaceId,
        Guid? userId,
        CancellationToken cancellationToken = default
    ) {
        if (file is null || file.Length == 0)
            throw new InvalidFormatException("Recipe import failed", "Upload a document file to import.");

        if (file.Length > MaxUploadBytes)
            throw new InvalidFormatException("Recipe import failed", $"Maximum upload size is {MaxUploadBytes} bytes.");

        var contentType = NormalizeContentType(file.ContentType);
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidFormatException(
                "Recipe import failed",
                "Unsupported file type. Use PDF, TXT, PNG, JPG/JPEG, or WEBP."
            );

        await using var stream = file.OpenReadStream();
        var extractedText = await ExtractTextAsync(stream, file.FileName, contentType, cancellationToken);
        if (string.IsNullOrWhiteSpace(extractedText))
            throw new InvalidFormatException("Recipe import failed", "No readable text was found in the uploaded file.");

        var sourceLabel = $"upload://{file.FileName}";
        return await recipeImportService.PreviewFromTextAsync(
            sourceLabel,
            extractedText,
            workspaceId,
            userId,
            cancellationToken
        );
    }

    private async Task<string> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken
    ) {
        if (contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
            return await ReadPlainTextAsync(fileStream, cancellationToken);

        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return ExtractPdfText(fileStream);

        return await ExtractImageTextWithOptionalExternalOcrAsync(fileStream, fileName, contentType, cancellationToken);
    }

    private static async Task<string> ReadPlainTextAsync(Stream stream, CancellationToken cancellationToken) {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string ExtractPdfText(Stream stream) {
        stream.Position = 0;
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages()) {
            var pageText = page.Text;
            if (string.IsNullOrWhiteSpace(pageText))
                continue;

            builder.AppendLine(pageText);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private async Task<string> ExtractImageTextWithOptionalExternalOcrAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken
    ) {
        var config = openAiOptions.Value;
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidFormatException(
                "Recipe import failed",
                "Image OCR requires an external OCR provider, but none is configured."
            );

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var base64 = Convert.ToBase64String(memory.ToArray());
        var dataUrl = $"data:{contentType};base64,{base64}";

        var endpoint = config.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var httpClient = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var payload = new {
            model = config.Model,
            temperature = 0.0,
            max_tokens = 2500,
            messages = new object[] {
                new {
                    role = "system",
                    content =
                        "Extract all recipe-related text from this image. Return only plain text, preserving ingredient and step ordering."
                },
                new {
                    role = "user",
                    content = new object[] {
                        new { type = "text", text = $"OCR this recipe image ({fileName}) and return plain text." },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) {
            logger.LogWarning(
                "External OCR request failed with status {StatusCode}.",
                (int)response.StatusCode
            );
            throw new InvalidFormatException(
                "Recipe import failed",
                "External OCR failed for the uploaded image."
            );
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var text = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return text?.Trim() ?? string.Empty;
    }

    private static string NormalizeContentType(string? contentType) {
        if (string.IsNullOrWhiteSpace(contentType))
            return string.Empty;

        var semicolonIndex = contentType.IndexOf(';');
        return (semicolonIndex >= 0 ? contentType[..semicolonIndex] : contentType).Trim().ToLowerInvariant();
    }
}
