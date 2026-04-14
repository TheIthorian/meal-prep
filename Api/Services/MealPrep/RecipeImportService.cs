using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text;
using Api.Data;
using Api.Domain;
using Api.Logging;
using Api.Models;
using Microsoft.Extensions.Http;

namespace Api.Services.MealPrep;

/// <summary>
///     Fetches and extracts recipe previews from external web pages.
/// </summary>
public class RecipeImportService(
    HttpClient httpClient,
    IHttpClientFactory httpClientFactory,
    MeasurementService measurementService,
    RecipeImportLlmParser recipeImportLlmParser,
    ApiDbContext db,
    ILogger<RecipeImportService> logger
)
{
    public const string RecipeImageImportHttpClientName = "RecipeImageImport";
    private const int MaxResponseBytes = 2 * 1024 * 1024;

    private static readonly Regex JsonLdScriptPattern = new(
        "<script[^>]*type=[\"']application/ld\\+json[\"'][^>]*>(?<json>.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
    );

    public Task<RecipeImportPreview> PreviewAsync(string url, CancellationToken cancellationToken = default) {
        return PreviewAsync(url, null, null, cancellationToken);
    }

    public async Task<RecipeImportPreview> PreviewAsync(
        string url,
        Guid? workspaceId,
        Guid? userId,
        CancellationToken cancellationToken = default
    ) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
            throw new InvalidFormatException("Recipe import failed", "The provided URL is not valid.");

        EnsureImportUrlIsAllowed(parsedUrl);

        using var request = new HttpRequestMessage(HttpMethod.Get, parsedUrl);
        request.Headers.UserAgent.ParseAdd("MealPrepBot/1.0");

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
            throw new InvalidFormatException(
                "Recipe import failed",
                $"The source page returned {(int)response.StatusCode} {response.ReasonPhrase}."
            );

        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            throw new InvalidFormatException(
                "Recipe import failed",
                "The source page is too large to import safely."
            );

        var html = await ReadContentWithLimitAsync(response.Content, cancellationToken);
        var structured = TryExtractStructuredRecipe(html, url);
        if (structured is not null) return structured;

        var heuristic = TryExtractHeuristicRecipe(html, url);
        if (heuristic is not null) return heuristic;

        using (logger.BeginPropertyScope(("sourceUrl", url))) {
            logger.LogInformation("Recipe import using LLM fallback.");
        }

        var llmInvocation = await recipeImportLlmParser.TryParseStructuredRecipeAsync(html, url, cancellationToken);

        if (llmInvocation is not null && workspaceId is not null)
            await TryPersistRecipeImportAiLogAsync(workspaceId.Value, userId, url, llmInvocation, cancellationToken);

        if (llmInvocation?.Structured is not null)
            return MapLlmStructuredPreview(llmInvocation.Structured, url, html);

        if (llmInvocation is not null && !string.IsNullOrWhiteSpace(llmInvocation.FailureDetail))
            throw new InvalidFormatException(
                "Recipe import failed",
                $"Structured metadata was missing and LLM fallback failed: {llmInvocation.FailureDetail}"
            );

        throw new InvalidFormatException(
            "Recipe import failed",
            "Could not find recipe metadata on that page. You can still create the recipe manually."
        );
    }

    /// <summary>
    ///     Downloads a recipe image from an absolute http(s) URL (redirects followed, size and content-type enforced).
    /// </summary>
    public async Task<ImportedRecipeImagePayload?> TryDownloadImportImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default
    ) {
        var inlinePayload = TryParseInlineImageDataUrl(imageUrl);
        if (inlinePayload is not null)
            return inlinePayload;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)) return null;

        var imageClient = httpClientFactory.CreateClient(RecipeImageImportHttpClientName);
        var currentUri = uri;

        for (var hop = 0; hop < 8; hop++) {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);

            using var response = await imageClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (IsRecipeImageRedirect(response.StatusCode)) {
                if (response.Headers.Location is null) return null;

                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);

                if (string.Equals(currentUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) {
                    currentUri = new UriBuilder(currentUri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
                }

                continue;
            }

            if (!response.IsSuccessStatusCode) return null;

            if (response.Content.Headers.ContentLength is > RecipeImageUploadConstants.MaxBytes) return null;

            var declaredType = response.Content.Headers.ContentType?.MediaType;
            var mediaType = RecipeImageUploadConstants.ResolveImportedImageContentType(
                declaredType,
                currentUri.AbsolutePath
            );
            if (mediaType is null) return null;

            await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var memory = new MemoryStream();
            var buffer = new byte[8192];
            long total = 0;

            int read;
            while ((read = await networkStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0) {
                total += read;
                if (total > RecipeImageUploadConstants.MaxBytes) return null;

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            var bytes = memory.ToArray();
            var fileName = RecipeImageUploadConstants.FileNameForUpload(
                Path.GetFileName(currentUri.LocalPath),
                mediaType
            );

            return new ImportedRecipeImagePayload(bytes, mediaType, fileName);
        }

        return null;
    }

    private static ImportedRecipeImagePayload? TryParseInlineImageDataUrl(string imageUrl) {
        if (string.IsNullOrWhiteSpace(imageUrl)
            || !imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var commaIndex = imageUrl.IndexOf(',');
        if (commaIndex < 0)
            return null;

        var header = imageUrl[..commaIndex];
        var payload = imageUrl[(commaIndex + 1)..];
        var mediaTypeToken = header["data:".Length..];
        var headerParts = mediaTypeToken.Split(';', StringSplitOptions.RemoveEmptyEntries);

        if (headerParts.Length == 0)
            return null;

        var mediaType = headerParts[0].Trim();
        if (!RecipeImageUploadConstants.IsAllowedContentType(mediaType))
            return null;

        var isBase64 = headerParts.Skip(1)
            .Any(value => string.Equals(value.Trim(), "base64", StringComparison.OrdinalIgnoreCase));
        if (!isBase64)
            return null;

        byte[] bytes;
        try {
            bytes = Convert.FromBase64String(payload.Trim());
        } catch (FormatException) {
            return null;
        }

        if (bytes.Length == 0 || bytes.Length > RecipeImageUploadConstants.MaxBytes)
            return null;

        var fileName = RecipeImageUploadConstants.FileNameForUpload("imported-image", mediaType);
        return new ImportedRecipeImagePayload(bytes, mediaType, fileName);
    }

    private static bool IsRecipeImageRedirect(HttpStatusCode statusCode) {
        return statusCode is HttpStatusCode.Moved
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private async Task TryPersistRecipeImportAiLogAsync(
        Guid workspaceId,
        Guid? userId,
        string sourceUrl,
        RecipeImportLlmInvocationResult invocation,
        CancellationToken cancellationToken
    ) {
        try {
            var row = new RecipeImportAiLog {
                WorkspaceId = workspaceId,
                UserId = userId,
                SourceUrl = sourceUrl,
                ProviderBaseUrl = invocation.ProviderBaseUrl,
                Model = invocation.Model,
                RequestJson = invocation.RequestJson,
                ResponseContent = invocation.ResponseContent,
                FinishReason = invocation.FinishReason,
                ParsedSuccessfully = invocation.ParsedSuccessfully,
                FailureDetail = invocation.FailureDetail
            };

            await db.RecipeImportAiLogs.AddAsync(row, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        } catch (Exception ex) {
            using var scope = logger.BeginPropertyScope(
                ("workspaceId", workspaceId),
                ("sourceUrl", sourceUrl)
            );

            logger.LogWarning(ex, "Failed to persist recipe import AI log.");
        }
    }

    private RecipeImportPreview MapLlmStructuredPreview(
        RecipeImportLlmStructuredDto dto,
        string sourceUrl,
        string html
    ) {
        var servings = dto.Servings > 0 ? dto.Servings : 1m;
        var title = string.IsNullOrWhiteSpace(dto.Title) ? BuildFallbackTitle(sourceUrl, html) : dto.Title.Trim();
        var description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        var tags = RecipeTagWhitelist.NormalizeToWhitelist(
                dto.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim())
            )
            .ToList();

        var ingredientLines = dto.IngredientLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var ingredients = ingredientLines
            .Select((text, index) => ParseIngredient(text, index))
            .ToArray();

        var stepTexts = dto.Steps
            .Select(step => step.Instruction.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        var steps = stepTexts
            .Select((step, index) => new RecipeImportPreviewStep(index, step, ParseTimerSeconds(step)))
            .ToArray();

        var nutrition = BuildNutritionPreviewFromLlm(dto.Nutrition ?? new RecipeImportLlmNutritionDto(), servings);

        var imageUrl = !string.IsNullOrWhiteSpace(dto.ImageUrl)
            ? ResolveToAbsoluteUri(dto.ImageUrl.Trim(), sourceUrl)
            : null;
        imageUrl ??= TryExtractOgImageFromHtml(html, sourceUrl);

        return new RecipeImportPreview(
            title,
            description,
            servings,
            sourceUrl,
            dto.PrepMinutes,
            dto.CookMinutes,
            tags,
            ingredients,
            steps,
            nutrition,
            imageUrl
        );
    }

    private static string BuildFallbackTitle(string sourceUrl, string html) {
        var htmlTitleMatch = Regex.Match(
            html,
            "<title[^>]*>(?<title>.*?)</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (htmlTitleMatch.Success) {
            var decoded = WebUtility.HtmlDecode(htmlTitleMatch.Groups["title"].Value);
            var normalized = Regex.Replace(decoded, "\\s+", " ").Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;
        }

        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return $"Imported recipe from {uri.Host}";

        return "Imported recipe";
    }

    private RecipeImportPreviewNutrition? BuildNutritionPreviewFromLlm(
        RecipeImportLlmNutritionDto nutrition,
        decimal servingBasis
    ) {
        var nutrients = new List<RecipeImportPreviewNutrient>();

        void TryAdd(string nutrientType, decimal? amount) {
            if (amount is not null) nutrients.Add(new RecipeImportPreviewNutrient(nutrientType, amount.Value));
        }

        TryAdd(RecipeNutrientTypes.Calories, nutrition.Calories);
        TryAdd(RecipeNutrientTypes.Protein, nutrition.Protein);
        TryAdd(RecipeNutrientTypes.Carbohydrate, nutrition.Carbohydrate);
        TryAdd(RecipeNutrientTypes.Fat, nutrition.Fat);
        TryAdd(RecipeNutrientTypes.Fiber, nutrition.Fiber);
        TryAdd(RecipeNutrientTypes.Sugar, nutrition.Sugar);
        TryAdd(RecipeNutrientTypes.Sodium, nutrition.Sodium);

        return nutrients.Count == 0
            ? null
            : new RecipeImportPreviewNutrition(servingBasis, nutrients);
    }

    private static void EnsureImportUrlIsAllowed(Uri parsedUrl) {
        if (!parsedUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !parsedUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidFormatException(
                "Recipe import failed",
                "Only http and https recipe URLs are supported."
            );

        if (!string.IsNullOrWhiteSpace(parsedUrl.UserInfo))
            throw new InvalidFormatException(
                "Recipe import failed",
                "Recipe import URLs cannot include embedded credentials."
            );

        if (IsBlockedHost(parsedUrl.Host))
            throw new InvalidFormatException(
                "Recipe import failed",
                "Local or private network recipe URLs are not allowed."
            );
    }

    private static bool IsBlockedHost(string host) {
        if (string.IsNullOrWhiteSpace(host)) return true;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ipAddress)) return false;

        if (IPAddress.IsLoopback(ipAddress)) return true;

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
            if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6Multicast || ipAddress.IsIPv6SiteLocal)
                return true;

            var bytes = ipAddress.GetAddressBytes();
            return bytes[0] == 0xfc || bytes[0] == 0xfd;
        }

        var ipv4Bytes = ipAddress.GetAddressBytes();
        return ipAddress.Equals(IPAddress.Any)
               || ipAddress.Equals(IPAddress.Broadcast)
               || ipv4Bytes[0] == 10
               || (ipv4Bytes[0] == 127)
               || (ipv4Bytes[0] == 169 && ipv4Bytes[1] == 254)
               || (ipv4Bytes[0] == 172 && ipv4Bytes[1] >= 16 && ipv4Bytes[1] <= 31)
               || (ipv4Bytes[0] == 192 && ipv4Bytes[1] == 168);
    }

    private static async Task<string> ReadContentWithLimitAsync(
        HttpContent content,
        CancellationToken cancellationToken
    ) {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[8192];
        var totalCharsRead = 0;
        var builder = new StringBuilder();

        while (true) {
            var charsRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (charsRead == 0) break;

            totalCharsRead += charsRead;
            if (totalCharsRead > MaxResponseBytes)
                throw new InvalidFormatException(
                    "Recipe import failed",
                    "The source page is too large to import safely."
                );

            builder.Append(buffer, 0, charsRead);
        }

        return builder.ToString();
    }

    private RecipeImportPreview? TryExtractStructuredRecipe(string html, string sourceUrl) {
        foreach (Match match in JsonLdScriptPattern.Matches(html)) {
            var rawJson = WebUtility.HtmlDecode(match.Groups["json"].Value);
            JsonNode? rootNode;

            try {
                rootNode = JsonNode.Parse(rawJson);
            } catch (JsonException) {
                continue;
            }

            var recipeNode = FindRecipeNode(rootNode);
            if (recipeNode is not JsonObject recipeObject) continue;

            var title = recipeObject["name"]?.GetValue<string>()?.Trim();
            var ingredientTexts = ReadStringList(recipeObject["recipeIngredient"]);
            var steps = ReadInstructionList(recipeObject["recipeInstructions"]);
            if (string.IsNullOrWhiteSpace(title) || ingredientTexts.Count == 0 || steps.Count == 0) continue;

            var servings = ParseServings(recipeObject["recipeYield"]);
            var prepMinutes = ParseDurationMinutes(recipeObject["prepTime"]?.GetValue<string>());
            var cookMinutes = ParseDurationMinutes(recipeObject["cookTime"]?.GetValue<string>());
            var tags = RecipeTagWhitelist.NormalizeToWhitelist(ReadTags(recipeObject));

            var ingredients = ingredientTexts
                .Select((text, index) => ParseIngredient(text, index))
                .ToArray();

            var nutritionObject = recipeObject["nutrition"] as JsonObject;
            var nutrition = nutritionObject is null ? null : BuildNutritionPreview(nutritionObject, servings);

            var imageUrl = TryExtractRecipeImageUrl(recipeObject, sourceUrl)
                           ?? TryExtractOgImageFromHtml(html, sourceUrl);

            return new RecipeImportPreview(
                title,
                recipeObject["description"]?.GetValue<string>()?.Trim(),
                servings ?? 1m,
                sourceUrl,
                prepMinutes,
                cookMinutes,
                tags,
                ingredients,
                steps.Select((step, index) => new RecipeImportPreviewStep(index, step, ParseTimerSeconds(step)))
                    .ToArray(),
                nutrition,
                imageUrl
            );
        }

        return null;
    }

    private RecipeImportPreview? TryExtractHeuristicRecipe(string html, string sourceUrl) {
        var title = ReadMetaContent(html, "property", "og:title")
                    ?? ReadMetaContent(html, "name", "twitter:title")
                    ?? ReadTitleTag(html);
        if (string.IsNullOrWhiteSpace(title)) return null;

        var ingredientTexts = ReadItemPropList(html, "recipeIngredient");
        var steps = ReadInstructionElements(html);
        if (ingredientTexts.Count == 0 || steps.Count == 0) return null;

        var imageUrl = TryExtractOgImageFromHtml(html, sourceUrl);

        return new RecipeImportPreview(
            title.Trim(),
            ReadMetaContent(html, "name", "description"),
            1m,
            sourceUrl,
            null,
            null,
            [],
            ingredientTexts.Select((text, index) => ParseIngredient(text, index)).ToArray(),
            steps.Select((step, index) => new RecipeImportPreviewStep(index, step, ParseTimerSeconds(step))).ToArray(),
            null,
            imageUrl
        );
    }

    private JsonNode? FindRecipeNode(JsonNode? node) {
        if (node is JsonObject jsonObject) {
            if (NodeIsRecipe(jsonObject)) return jsonObject;

            if (jsonObject["@graph"] is JsonArray graphArray) {
                foreach (var item in graphArray) {
                    var graphRecipe = FindRecipeNode(item);
                    if (graphRecipe is not null) return graphRecipe;
                }
            }

            foreach (var child in jsonObject) {
                var recipe = FindRecipeNode(child.Value);
                if (recipe is not null) return recipe;
            }
        }

        if (node is JsonArray jsonArray) {
            foreach (var item in jsonArray) {
                var recipe = FindRecipeNode(item);
                if (recipe is not null) return recipe;
            }
        }

        return null;
    }

    private static bool NodeIsRecipe(JsonObject jsonObject) {
        if (jsonObject["@type"] is JsonValue typeValue)
            return string.Equals(typeValue.GetValue<string>(), "Recipe", StringComparison.OrdinalIgnoreCase);

        if (jsonObject["@type"] is JsonArray typeArray)
            return typeArray.OfType<JsonValue>()
                .Select(value => value.GetValue<string>())
                .Any(type => string.Equals(type, "Recipe", StringComparison.OrdinalIgnoreCase));

        return false;
    }

    private RecipeImportPreviewIngredient ParseIngredient(string text, int index) {
        var trimmed = WebUtility.HtmlDecode(text).Trim();
        var match = Regex.Match(
            trimmed,
            "^(?<amount>[0-9]+\\s+[0-9]+/[0-9]+|[0-9]+\\s+[¼½¾]|[0-9]+/[0-9]+|[0-9]+(?:\\.[0-9]+)?|[¼½¾])?\\s*(?<unit>[A-Za-z]+(?:\\s?oz)?)?\\s*(?<name>.+)$"
        );

        var amount = measurementService.ParseDecimal(match.Groups["amount"].Value);
        var unit = string.IsNullOrWhiteSpace(match.Groups["unit"].Value) ? null : match.Groups["unit"].Value.Trim();
        var name = match.Groups["name"].Value.Trim();

        return new RecipeImportPreviewIngredient(
            index,
            name,
            measurementService.NormalizeIngredientName(name),
            amount,
            unit,
            null,
            null,
            measurementService.BuildDisplayText(amount, unit, name, null)
        );
    }

    private static List<string> ReadStringList(JsonNode? node) {
        var values = new List<string>();
        if (node is JsonArray array)
            values.AddRange(
                array.OfType<JsonValue>()
                    .Select(value => value.GetValue<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
            );

        return values;
    }

    private static List<string> ReadInstructionList(JsonNode? node) {
        var steps = new List<string>();

        switch (node) {
            case JsonValue jsonValue:
                steps.Add(jsonValue.GetValue<string>());
                break;
            case JsonArray jsonArray:
                foreach (var child in jsonArray) {
                    if (child is JsonValue childValue)
                        steps.Add(childValue.GetValue<string>());
                    else if (child is JsonObject childObject) {
                        if (childObject["text"]?.GetValue<string>() is string stepText
                            && !string.IsNullOrWhiteSpace(stepText))
                            steps.Add(stepText.Trim());

                        if (childObject["itemListElement"] is JsonArray itemList)
                            steps.AddRange(ReadInstructionList(itemList));
                    }
                }

                break;
        }

        return steps.Where(step => !string.IsNullOrWhiteSpace(step)).ToList();
    }

    private static List<string> ReadTags(JsonObject recipeObject) {
        var tags = new List<string>();

        if (recipeObject["keywords"]?.GetValue<string>() is string keywords)
            tags.AddRange(keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (recipeObject["recipeCategory"] is JsonValue categoryValue)
            tags.Add(categoryValue.GetValue<string>());

        if (recipeObject["recipeCategory"] is JsonArray categoryArray)
            tags.AddRange(categoryArray.OfType<JsonValue>().Select(value => value.GetValue<string>()));

        return tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag)
            .ToList();
    }

    private static decimal? ParseServings(JsonNode? node) {
        if (node is null) return null;

        if (node is JsonArray jsonArray)
            return ParseServings(jsonArray.FirstOrDefault());

        if (node is not JsonValue jsonValue)
            return null;

        if (jsonValue.TryGetValue<decimal>(out var dec))
            return dec > 0 ? dec : null;

        if (jsonValue.TryGetValue<int>(out var intVal))
            return intVal > 0 ? intVal : null;

        if (jsonValue.TryGetValue<long>(out var longVal))
            return longVal > 0 ? longVal : null;

        if (jsonValue.TryGetValue<double>(out var dbl))
            return dbl > 0 ? (decimal)dbl : null;

        if (!jsonValue.TryGetValue<string>(out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var match = Regex.Match(raw, "[0-9]+(?:\\.[0-9]+)?");
        return match.Success && decimal.TryParse(match.Value, out var servings) ? servings : null;
    }

    private static int? ParseDurationMinutes(string? isoDuration) {
        if (string.IsNullOrWhiteSpace(isoDuration)) return null;

        var hoursMatch = Regex.Match(isoDuration, "(?<hours>[0-9]+)H", RegexOptions.IgnoreCase);
        var minutesMatch = Regex.Match(isoDuration, "(?<minutes>[0-9]+)M", RegexOptions.IgnoreCase);

        var hours = hoursMatch.Success ? int.Parse(hoursMatch.Groups["hours"].Value) : 0;
        var minutes = minutesMatch.Success ? int.Parse(minutesMatch.Groups["minutes"].Value) : 0;
        return hours == 0 && minutes == 0 ? null : (hours * 60) + minutes;
    }

    private static decimal? ParseNumber(JsonNode? node) {
        if (node is null) return null;

        if (node is JsonValue jsonValue) {
            if (jsonValue.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
            if (jsonValue.TryGetValue<string>(out var stringValue)) {
                var match = Regex.Match(stringValue, "[0-9]+(?:\\.[0-9]+)?");
                return match.Success && decimal.TryParse(match.Value, out var parsed) ? parsed : null;
            }
        }

        return null;
    }

    private static RecipeImportPreviewNutrition? BuildNutritionPreview(
        JsonObject nutritionObject,
        decimal? servingBasis
    ) {
        var nutrients = new List<RecipeImportPreviewNutrient>();

        TryAddNutrient(nutrients, RecipeNutrientTypes.Calories, nutritionObject["calories"]);
        TryAddNutrient(nutrients, RecipeNutrientTypes.Protein, nutritionObject["proteinContent"]);
        TryAddNutrient(nutrients, RecipeNutrientTypes.Carbohydrate, nutritionObject["carbohydrateContent"]);
        TryAddNutrient(nutrients, RecipeNutrientTypes.Fat, nutritionObject["fatContent"]);
        TryAddNutrient(nutrients, RecipeNutrientTypes.Fiber, nutritionObject["fiberContent"]);
        TryAddNutrient(nutrients, RecipeNutrientTypes.Sugar, nutritionObject["sugarContent"]);
        TryAddNutrient(nutrients, RecipeNutrientTypes.Sodium, nutritionObject["sodiumContent"]);

        return nutrients.Count == 0 && servingBasis is null
            ? null
            : new RecipeImportPreviewNutrition(servingBasis, nutrients);
    }

    private static void TryAddNutrient(
        List<RecipeImportPreviewNutrient> nutrients,
        string nutrientType,
        JsonNode? node
    ) {
        var amount = ParseNumber(node);
        if (amount is not null) nutrients.Add(new RecipeImportPreviewNutrient(nutrientType, amount.Value));
    }

    private static int? ParseTimerSeconds(string step) {
        var minuteMatch = Regex.Match(step, "(?<minutes>[0-9]+)\\s*(minutes|minute|min)", RegexOptions.IgnoreCase);
        if (minuteMatch.Success) return int.Parse(minuteMatch.Groups["minutes"].Value) * 60;

        var secondMatch = Regex.Match(step, "(?<seconds>[0-9]+)\\s*(seconds|second|sec)", RegexOptions.IgnoreCase);
        return secondMatch.Success ? int.Parse(secondMatch.Groups["seconds"].Value) : null;
    }

    private static string? ResolveToAbsoluteUri(string? raw, string sourceUrl) {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)) return absolute.ToString();

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var baseUri)) return null;

        return Uri.TryCreate(baseUri, trimmed, out var combined) ? combined.ToString() : null;
    }

    private static string? TryExtractRecipeImageUrl(JsonObject recipeObject, string sourceUrl) {
        var imageNode = recipeObject["image"];
        if (imageNode is null) return null;

        var candidates = new List<string>();

        switch (imageNode) {
            case JsonValue jsonValue:
                if (jsonValue.TryGetValue<string>(out var single)) candidates.Add(single.Trim());

                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray) CollectImageUrlsFromNode(item, candidates);

                break;
            case JsonObject jsonObject:
                CollectImageUrlsFromNode(jsonObject, candidates);

                break;
        }

        foreach (var raw in candidates.Where(value => !string.IsNullOrWhiteSpace(value))) {
            var resolved = ResolveToAbsoluteUri(raw, sourceUrl);
            if (resolved is not null) return resolved;
        }

        return null;
    }

    private static void CollectImageUrlsFromNode(JsonNode? node, List<string> candidates) {
        switch (node) {
            case JsonValue jsonValue:
                if (jsonValue.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) candidates.Add(s.Trim());

                break;
            case JsonObject jsonObject:
                foreach (var key in new[] { "url", "contentUrl" }) {
                    if (jsonObject[key] is JsonValue urlValue
                        && urlValue.TryGetValue<string>(out var url)
                        && !string.IsNullOrWhiteSpace(url))
                        candidates.Add(url.Trim());
                }

                break;
        }
    }

    private static string? TryExtractOgImageFromHtml(string html, string sourceUrl) {
        var raw = ReadMetaContent(html, "property", "og:image")
                  ?? ReadMetaContent(html, "property", "og:image:url")
                  ?? ReadMetaContent(html, "name", "twitter:image");

        return ResolveToAbsoluteUri(raw, sourceUrl);
    }

    private static string? ReadMetaContent(string html, string attributeName, string attributeValue) {
        var match = Regex.Match(
            html,
            $"<meta[^>]*{attributeName}=[\"']{Regex.Escape(attributeValue)}[\"'][^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*>",
            RegexOptions.IgnoreCase
        );

        return match.Success ? WebUtility.HtmlDecode(match.Groups["content"].Value).Trim() : null;
    }

    private static string? ReadTitleTag(string html) {
        var match = Regex.Match(
            html,
            "<title>(?<title>.*?)</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        return match.Success ? WebUtility.HtmlDecode(match.Groups["title"].Value).Trim() : null;
    }

    private static List<string> ReadItemPropList(string html, string itemProp) {
        var matches = Regex.Matches(
            html,
            $"<[^>]*itemprop=[\"']{Regex.Escape(itemProp)}[\"'][^>]*>(?<content>.*?)</[^>]+>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        return matches
            .Select(match => StripHtml(match.Groups["content"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static List<string> ReadInstructionElements(string html) {
        var values = ReadItemPropList(html, "recipeInstructions");
        return values.Count > 0
            ? values
            : Regex.Matches(
                    html,
                    "<li[^>]*>(?<content>.*?)</li>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline
                )
                .Select(match => StripHtml(match.Groups["content"].Value))
                .Where(text => text.Length > 30)
                .Take(20)
                .ToList();
    }

    private static string StripHtml(string html) {
        var noTags = Regex.Replace(html, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(Regex.Replace(noTags, "\\s+", " ")).Trim();
    }
}

public sealed record RecipeImportPreview(
    string Title,
    string? Description,
    decimal Servings,
    string SourceUrl,
    int? PrepMinutes,
    int? CookMinutes,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<RecipeImportPreviewIngredient> Ingredients,
    IReadOnlyCollection<RecipeImportPreviewStep> Steps,
    RecipeImportPreviewNutrition? Nutrition,
    string? ImageUrl
);

/// <summary>
///     Raw image bytes downloaded during recipe import, ready to upload to object storage.
/// </summary>
public sealed record ImportedRecipeImagePayload(byte[] Data, string ContentType, string FileName);

public sealed record RecipeImportPreviewIngredient(
    int SortOrder,
    string Name,
    string? NormalizedIngredientName,
    decimal? Amount,
    string? Unit,
    string? PreparationNote,
    string? Section,
    string DisplayText
);

public sealed record RecipeImportPreviewStep(int SortOrder, string Instruction, int? TimerSeconds);

public sealed record RecipeImportPreviewNutrition(
    decimal? ServingBasis,
    IReadOnlyCollection<RecipeImportPreviewNutrient> Nutrients
);

public sealed record RecipeImportPreviewNutrient(string NutrientType, decimal Amount);
