using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api.Configuration;
using Api.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Api.Services.MealPrep;

/// <summary>
///     Calls an OpenAI-compatible chat API (OpenRouter by default) with structured JSON output when HTML heuristics fail.
/// </summary>
public sealed class RecipeImportLlmParser
{
    private const int MaxHtmlChars = 120_000;
    private const int MaxTextChars = 80_000;

    private static string BuildRecipeImportSystemPrompt() {
        return $"""
                You extract one primary recipe from web page HTML. Use only information supported by the HTML.
                If the page has no real recipe, set title to an empty string, ingredientLines and steps to empty arrays, and description to explain briefly that no recipe was found.
                Ingredient lines must be plain text as a cook would list them (quantity, unit, name).
                Instructions must be ordered cooking steps. Omit nutrition values unless clearly stated.

                Tags: choose zero to twelve values ONLY from this list. Copy each tag exactly as written (kebab-case). Do not invent tags outside this list.
                {RecipeTagWhitelist.FormatForPrompt()}
                """;
    }

    private static string BuildRecipeImportTextSystemPrompt() {
        return $"""
                You extract one primary recipe from OCR or plain document text. Use only information supported by the provided text.
                If the text has no real recipe, set title to an empty string, ingredientLines and steps to empty arrays, and description to explain briefly that no recipe was found.
                Ingredient lines must be plain text as a cook would list them (quantity, unit, name).
                Instructions must be ordered cooking steps. Omit nutrition values unless clearly stated.
                Ignore OCR junk fragments that are not complete recipe content (single letters, broken word shards, isolated symbols, page furniture).
                Do not include partial or ambiguous ingredient fragments. Keep only complete ingredient lines with clear food meaning.
                If OCR split one ingredient across lines and it is obvious, merge it into one ingredient line.

                Tags: choose zero to twelve values ONLY from this list. Copy each tag exactly as written (kebab-case). Do not invent tags outside this list.
                {RecipeTagWhitelist.FormatForPrompt()}
                """;
    }

    private static readonly BinaryData RecipeJsonSchema = BinaryData.FromString(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "description": { "type": "string" },
            "servings": { "type": "number" },
            "prepMinutes": { "type": ["integer", "null"] },
            "cookMinutes": { "type": ["integer", "null"] },
            "tags": {
              "type": "array",
              "items": { "type": "string" }
            },
            "ingredientLines": {
              "type": "array",
              "items": { "type": "string" }
            },
            "steps": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "instruction": { "type": "string" }
                },
                "required": ["instruction"],
                "additionalProperties": false
              }
            },
            "nutrition": {
              "type": "object",
              "properties": {
                "calories": { "type": ["number", "null"] },
                "protein": { "type": ["number", "null"] },
                "carbohydrate": { "type": ["number", "null"] },
                "fat": { "type": ["number", "null"] },
                "fiber": { "type": ["number", "null"] },
                "sugar": { "type": ["number", "null"] },
                "sodium": { "type": ["number", "null"] }
              },
              "required": ["calories", "protein", "carbohydrate", "fat", "fiber", "sugar", "sodium"],
              "additionalProperties": false
            },
            "imageUrl": { "type": ["string", "null"] }
          },
          "required": [
            "title",
            "description",
            "servings",
            "prepMinutes",
            "cookMinutes",
            "tags",
            "ingredientLines",
            "steps",
            "nutrition",
            "imageUrl"
          ],
          "additionalProperties": false
        }
        """
    );

    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static readonly JsonSerializerOptions RequestLogSerializerOptions = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false
    };

    private readonly ChatClient? _chatClient;
    private readonly ILogger<RecipeImportLlmParser> _logger;
    private readonly string _providerBaseUrl;
    private readonly string _model;

    public RecipeImportLlmParser(IOptions<OpenAIConfiguration> options, ILogger<RecipeImportLlmParser> logger) {
        _logger = logger;
        var cfg = options.Value;
        _providerBaseUrl = cfg.BaseUrl.Trim();
        _model = cfg.Model.Trim();

        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            return;

        if (!Uri.TryCreate(cfg.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var endpoint)) {
            _logger.LogError("OpenAI BaseUrl is not a valid absolute URI.");

            return;
        }

        var clientOptions = new OpenAIClientOptions { Endpoint = endpoint };
        _chatClient = new ChatClient(cfg.Model, new ApiKeyCredential(cfg.ApiKey), clientOptions);
    }

    /// <summary>
    ///     Returns null when LLM import is not configured. Otherwise returns request/response details and optional parsed recipe.
    /// </summary>
    internal async Task<RecipeImportLlmInvocationResult?> TryParseStructuredRecipeAsync(
        string html,
        string sourceUrl,
        CancellationToken cancellationToken = default
    ) {
        if (_chatClient is null) {
            _logger.LogWarning("Recipe import LLM parser is not configured; OpenAI API key is missing.");

            return new RecipeImportLlmInvocationResult(
                null,
                _providerBaseUrl,
                _model,
                BuildRequestJson(sourceUrl, "", BuildRecipeImportSystemPrompt()),
                null,
                null,
                false,
                "LLM parser is not configured (missing OpenAI API key)."
            );
        }

        using var scope = _logger.BeginPropertyScope(("sourceUrl", sourceUrl));

        var prepared = PrepareHtmlForPrompt(html);
        if (string.IsNullOrWhiteSpace(prepared)) {
            _logger.LogWarning("Recipe import LLM skipped: no HTML content after preparation.");

            return new RecipeImportLlmInvocationResult(
                null,
                _providerBaseUrl,
                _model,
                BuildRequestJson(sourceUrl, "", BuildRecipeImportSystemPrompt()),
                null,
                null,
                false,
                "No HTML content after removing scripts/styles."
            );
        }

        var userContent = $"""
                           Source URL: {sourceUrl}

                           HTML:
                           {prepared}
                           """;

        var systemPrompt = BuildRecipeImportSystemPrompt();
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt), new UserChatMessage(userContent) };

        var options = new ChatCompletionOptions {
            Temperature = 0.2f,
            MaxOutputTokenCount = 4096,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "recipe_import",
                jsonSchema: RecipeJsonSchema,
                jsonSchemaIsStrict: true
            )
        };

        var requestJson = BuildRequestJson(sourceUrl, userContent, systemPrompt);

        try {
            ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(
                messages,
                options,
                cancellationToken
            );
            var completion = result.Value;
            var finishReason = completion.FinishReason.ToString();

            if (!string.IsNullOrWhiteSpace(completion.Refusal)) {
                _logger.LogWarning("Recipe import LLM refused to respond.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    completion.Refusal,
                    finishReason,
                    false,
                    "Model returned a refusal instead of recipe JSON."
                );
            }

            if (completion.Content.Count == 0) {
                _logger.LogWarning("Recipe import LLM returned no message content parts.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    null,
                    finishReason,
                    false,
                    "No message content parts in the completion."
                );
            }

            var text = completion.Content[0].Text;
            if (string.IsNullOrWhiteSpace(text)) {
                _logger.LogWarning("Recipe import LLM returned empty content.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    text,
                    finishReason,
                    false,
                    "Assistant message text was empty."
                );
            }

            if (!TryParseStructuredDto(text, out var dto, out var parseFailureDetail)) {
                _logger.LogWarning("Recipe import LLM returned JSON that could not be validated.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    text,
                    finishReason,
                    false,
                    parseFailureDetail ?? "Failed to deserialize LLM JSON payload."
                );
            }

            if (!IsPlausibleRecipe(dto, out var plausibilityFailureDetail)) {
                _logger.LogWarning("Recipe import LLM returned JSON that failed plausibility checks.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    text,
                    finishReason,
                    false,
                    plausibilityFailureDetail
                );
            }

            return new RecipeImportLlmInvocationResult(
                dto,
                _providerBaseUrl,
                _model,
                requestJson,
                text,
                finishReason,
                true,
                null
            );
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Recipe import LLM request failed.");

            return new RecipeImportLlmInvocationResult(
                null,
                _providerBaseUrl,
                _model,
                requestJson,
                null,
                null,
                false,
                TruncateForFailureDetail(ex.ToString())
            );
        }
    }

    /// <summary>
    ///     Parses OCR or plain document text into the recipe import schema.
    /// </summary>
    internal async Task<RecipeImportLlmInvocationResult?> TryParseStructuredRecipeFromTextAsync(
        string text,
        string sourceLabel,
        CancellationToken cancellationToken = default
    ) {
        if (_chatClient is null) {
            _logger.LogWarning("Recipe import LLM parser is not configured; OpenAI API key is missing.");

            return new RecipeImportLlmInvocationResult(
                null,
                _providerBaseUrl,
                _model,
                BuildRequestJson(sourceLabel, "", BuildRecipeImportTextSystemPrompt()),
                null,
                null,
                false,
                "LLM parser is not configured (missing OpenAI API key)."
            );
        }

        using var scope = _logger.BeginPropertyScope(("sourceLabel", sourceLabel));

        var prepared = PrepareTextForPrompt(text);
        if (string.IsNullOrWhiteSpace(prepared)) {
            _logger.LogWarning("Recipe import LLM skipped: no document text after preparation.");

            return new RecipeImportLlmInvocationResult(
                null,
                _providerBaseUrl,
                _model,
                BuildRequestJson(sourceLabel, "", BuildRecipeImportTextSystemPrompt()),
                null,
                null,
                false,
                "No document text remained after preparation."
            );
        }

        var userContent = $"""
                           Source: {sourceLabel}

                           Document text:
                           {prepared}
                           """;

        var systemPrompt = BuildRecipeImportTextSystemPrompt();
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt), new UserChatMessage(userContent) };

        var options = new ChatCompletionOptions {
            Temperature = 0.2f,
            MaxOutputTokenCount = 4096,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "recipe_import",
                jsonSchema: RecipeJsonSchema,
                jsonSchemaIsStrict: true
            )
        };

        var requestJson = BuildRequestJson(sourceLabel, userContent, systemPrompt);

        try {
            ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(
                messages,
                options,
                cancellationToken
            );
            var completion = result.Value;
            var finishReason = completion.FinishReason.ToString();

            if (!string.IsNullOrWhiteSpace(completion.Refusal)) {
                _logger.LogWarning("Recipe import LLM refused to respond.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    completion.Refusal,
                    finishReason,
                    false,
                    "Model returned a refusal instead of recipe JSON."
                );
            }

            if (completion.Content.Count == 0) {
                _logger.LogWarning("Recipe import LLM returned no message content parts.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    null,
                    finishReason,
                    false,
                    "No message content parts in the completion."
                );
            }

            var responseText = completion.Content[0].Text;
            if (string.IsNullOrWhiteSpace(responseText)) {
                _logger.LogWarning("Recipe import LLM returned empty content.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    responseText,
                    finishReason,
                    false,
                    "Assistant message text was empty."
                );
            }

            if (!TryParseStructuredDto(responseText, out var dto, out var parseFailureDetail)) {
                _logger.LogWarning("Recipe import LLM returned JSON that could not be validated.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    responseText,
                    finishReason,
                    false,
                    parseFailureDetail ?? "Failed to deserialize LLM JSON payload."
                );
            }

            if (!IsPlausibleRecipe(dto, out var plausibilityFailureDetail)) {
                _logger.LogWarning("Recipe import LLM returned JSON that failed plausibility checks.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    responseText,
                    finishReason,
                    false,
                    plausibilityFailureDetail
                );
            }

            return new RecipeImportLlmInvocationResult(
                dto,
                _providerBaseUrl,
                _model,
                requestJson,
                responseText,
                finishReason,
                true,
                null
            );
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Recipe import LLM request failed.");

            return new RecipeImportLlmInvocationResult(
                null,
                _providerBaseUrl,
                _model,
                requestJson,
                null,
                null,
                false,
                TruncateForFailureDetail(ex.ToString())
            );
        }
    }

    private static string BuildRequestJson(string sourceUrl, string userMessageContent, string systemPrompt) {
        var payload = new RecipeImportLlmRequestLogDto(
            [
                new RecipeImportLlmMessageLogDto("system", systemPrompt),
                new RecipeImportLlmMessageLogDto("user", userMessageContent)
            ],
            new RecipeImportLlmOptionsLogDto(
                0.2,
                4096,
                "json_schema",
                "recipe_import",
                true
            ),
            sourceUrl
        );

        return JsonSerializer.Serialize(payload, RequestLogSerializerOptions);
    }

    private static string TruncateForFailureDetail(string detail, int maxChars = 8000) {
        if (detail.Length <= maxChars)
            return detail;

        return detail[..maxChars] + "…";
    }

    private static bool IsPlausibleRecipe(RecipeImportLlmStructuredDto dto, out string failureDetail) {
        var hasTitle = !string.IsNullOrWhiteSpace(dto.Title);
        var hasIngredients = dto.IngredientLines.Any(static line => !string.IsNullOrWhiteSpace(line));
        var hasSteps = dto.Steps.Any(static step => !string.IsNullOrWhiteSpace(step.Instruction));
        var ingredientCount = dto.IngredientLines.Count;
        var stepCount = dto.Steps.Count;
        var nonEmptyIngredientCount = dto.IngredientLines.Count(static line => !string.IsNullOrWhiteSpace(line));
        var nonEmptyStepCount = dto.Steps.Count(static step => !string.IsNullOrWhiteSpace(step.Instruction));

        if (!hasIngredients && !hasSteps) {
            failureDetail =
                $"Plausibility check failed: no usable ingredients or steps. titlePresent={hasTitle}, ingredients={ingredientCount}, nonEmptyIngredients={nonEmptyIngredientCount}, steps={stepCount}, nonEmptySteps={nonEmptyStepCount}.";
            return false;
        }

        failureDetail =
            $"Plausibility check passed. titlePresent={hasTitle}, ingredients={ingredientCount}, nonEmptyIngredients={nonEmptyIngredientCount}, steps={stepCount}, nonEmptySteps={nonEmptyStepCount}.";
        return true;
    }

    private static bool TryParseStructuredDto(
        string rawText,
        out RecipeImportLlmStructuredDto dto,
        out string? failureDetail
    ) {
        dto = new RecipeImportLlmStructuredDto();
        failureDetail = null;

        var candidates = new List<string> { rawText.Trim() };

        var objectStart = rawText.IndexOf('{');
        var objectEnd = rawText.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
            candidates.Add(rawText[objectStart..(objectEnd + 1)].Trim());

        foreach (var candidate in candidates.Distinct()) {
            try {
                var parsed = JsonSerializer.Deserialize<RecipeImportLlmStructuredDto>(candidate, SerializerOptions);
                if (parsed is null)
                    continue;

                parsed.Tags ??= [];
                parsed.IngredientLines ??= [];
                parsed.Steps ??= [];
                parsed.Nutrition ??= new RecipeImportLlmNutritionDto();

                dto = parsed;
                return true;
            } catch (JsonException ex) {
                var snippet = candidate.Length > 500 ? candidate[..500] + "..." : candidate;
                failureDetail = $"Failed to parse LLM JSON: {ex.Message}. Candidate snippet: {snippet}";
            }
        }

        return false;
    }

    private static string PrepareHtmlForPrompt(string html) {
        var stripped = Regex.Replace(
            html,
            @"<script\b[^>]*>[\s\S]*?</script>",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
        );
        stripped = Regex.Replace(
            stripped,
            @"<style\b[^>]*>[\s\S]*?</style>",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
        );

        if (stripped.Length <= MaxHtmlChars)
            return stripped;

        return stripped[..MaxHtmlChars];
    }

    private static string PrepareTextForPrompt(string text) {
        var normalized = Regex.Replace(text, @"\r\n?", "\n");
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n").Trim();

        if (normalized.Length <= MaxTextChars)
            return normalized;

        return normalized[..MaxTextChars];
    }
}

internal sealed record RecipeImportLlmMessageLogDto(string Role, string Content);

internal sealed record RecipeImportLlmOptionsLogDto(
    double Temperature,
    int MaxOutputTokenCount,
    string ResponseFormatKind,
    string JsonSchemaName,
    bool JsonSchemaStrict
);

internal sealed record RecipeImportLlmRequestLogDto(
    IReadOnlyList<RecipeImportLlmMessageLogDto> Messages,
    RecipeImportLlmOptionsLogDto Options,
    string SourceUrl
);

/// <summary>
///     Structured recipe payload produced by the LLM (JSON schema). Internal to the meal-prep import flow.
/// </summary>
internal sealed class RecipeImportLlmStructuredDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> IngredientLines { get; set; } = [];
    public List<RecipeImportLlmStepDto> Steps { get; set; } = [];
    public RecipeImportLlmNutritionDto Nutrition { get; set; } = new();
    public string? ImageUrl { get; set; }
}

internal sealed class RecipeImportLlmStepDto
{
    public string Instruction { get; set; } = string.Empty;
}

internal sealed class RecipeImportLlmNutritionDto
{
    public decimal? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbohydrate { get; set; }
    public decimal? Fat { get; set; }
    public decimal? Fiber { get; set; }
    public decimal? Sugar { get; set; }
    public decimal? Sodium { get; set; }
}
