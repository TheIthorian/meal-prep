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
    private const string SystemPrompt = """
        You extract one primary recipe from web page HTML. Use only information supported by the HTML.
        If the page has no real recipe, set title to an empty string, ingredientLines and steps to empty arrays, and description to explain briefly that no recipe was found.
        Ingredient lines must be plain text as a cook would list them (quantity, unit, name).
        Instructions must be ordered cooking steps. Omit nutrition values unless clearly stated.
        """;

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
            }
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
            "nutrition"
          ],
          "additionalProperties": false
        }
        """
    );

    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions RequestLogSerializerOptions = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly ChatClient? _chatClient;
    private readonly ILogger<RecipeImportLlmParser> _logger;
    private readonly string _providerBaseUrl;
    private readonly string _model;

    public RecipeImportLlmParser(IOptions<OpenAIConfiguration> options, ILogger<RecipeImportLlmParser> logger)
    {
        _logger = logger;
        var cfg = options.Value;
        _providerBaseUrl = cfg.BaseUrl.Trim();
        _model = cfg.Model.Trim();

        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            return;

        if (!Uri.TryCreate(cfg.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var endpoint))
        {
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
    )
    {
        if (_chatClient is null)
            return null;

        using var scope = _logger.BeginPropertyScope(("sourceUrl", sourceUrl));

        var prepared = PrepareHtmlForPrompt(html);
        if (string.IsNullOrWhiteSpace(prepared))
        {
            _logger.LogWarning("Recipe import LLM skipped: no HTML content after preparation.");

            return new RecipeImportLlmInvocationResult(
                null,
                _providerBaseUrl,
                _model,
                BuildRequestJson(sourceUrl, ""),
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

        var messages = new List<ChatMessage> {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userContent)
        };

        var options = new ChatCompletionOptions {
            Temperature = 0.2f,
            MaxOutputTokenCount = 4096,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "recipe_import",
                jsonSchema: RecipeJsonSchema,
                jsonSchemaIsStrict: true
            )
        };

        var requestJson = BuildRequestJson(sourceUrl, userContent);

        try
        {
            ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var completion = result.Value;
            var finishReason = completion.FinishReason.ToString();

            if (!string.IsNullOrWhiteSpace(completion.Refusal))
            {
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

            if (completion.Content.Count == 0)
            {
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
            if (string.IsNullOrWhiteSpace(text))
            {
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

            var dto = JsonSerializer.Deserialize<RecipeImportLlmStructuredDto>(text, SerializerOptions);
            if (dto is null || !IsPlausibleRecipe(dto))
            {
                _logger.LogWarning("Recipe import LLM returned JSON that could not be validated.");

                return new RecipeImportLlmInvocationResult(
                    null,
                    _providerBaseUrl,
                    _model,
                    requestJson,
                    text,
                    finishReason,
                    false,
                    "Deserialized JSON was null or failed plausibility checks (title, ingredients, steps)."
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
        }
        catch (Exception ex)
        {
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

    private static string BuildRequestJson(string sourceUrl, string userMessageContent)
    {
        var payload = new RecipeImportLlmRequestLogDto(
            [
                new RecipeImportLlmMessageLogDto("system", SystemPrompt),
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

    private static string TruncateForFailureDetail(string detail, int maxChars = 8000)
    {
        if (detail.Length <= maxChars)
            return detail;

        return detail[..maxChars] + "…";
    }

    private static bool IsPlausibleRecipe(RecipeImportLlmStructuredDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return false;

        if (dto.IngredientLines.Count == 0 || dto.Steps.Count == 0)
            return false;

        return dto.IngredientLines.Any(static line => !string.IsNullOrWhiteSpace(line))
               && dto.Steps.Any(static step => !string.IsNullOrWhiteSpace(step.Instruction));
    }

    private static string PrepareHtmlForPrompt(string html)
    {
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
