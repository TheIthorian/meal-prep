using System.ClientModel;
using System.Text.Json;
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

    private readonly ChatClient? _chatClient;
    private readonly ILogger<RecipeImportLlmParser> _logger;

    public RecipeImportLlmParser(IOptions<OpenAIConfiguration> options, ILogger<RecipeImportLlmParser> logger)
    {
        _logger = logger;
        var cfg = options.Value;
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
    ///     Returns structured recipe fields from page HTML, or null when LLM import is not configured or parsing fails.
    /// </summary>
    internal async Task<RecipeImportLlmStructuredDto?> TryParseStructuredRecipeAsync(
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

            return null;
        }

        var messages = new List<ChatMessage> {
            new SystemChatMessage(
                """
                You extract one primary recipe from web page HTML. Use only information supported by the HTML.
                If the page has no real recipe, set title to an empty string, ingredientLines and steps to empty arrays, and description to explain briefly that no recipe was found.
                Ingredient lines must be plain text as a cook would list them (quantity, unit, name).
                Instructions must be ordered cooking steps. Omit nutrition values unless clearly stated.
                """
            ),
            new UserChatMessage(
                $"""
                 Source URL: {sourceUrl}

                 HTML:
                 {prepared}
                 """
            )
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

        try
        {
            ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var completion = result.Value;
            if (!string.IsNullOrWhiteSpace(completion.Refusal))
            {
                _logger.LogWarning("Recipe import LLM refused to respond.");

                return null;
            }

            if (completion.Content.Count == 0)
            {
                _logger.LogWarning("Recipe import LLM returned no message content parts.");

                return null;
            }

            var text = completion.Content[0].Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Recipe import LLM returned empty content.");

                return null;
            }

            var dto = JsonSerializer.Deserialize<RecipeImportLlmStructuredDto>(text, SerializerOptions);
            if (dto is null || !IsPlausibleRecipe(dto))
            {
                _logger.LogWarning("Recipe import LLM returned JSON that could not be validated.");

                return null;
            }

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recipe import LLM request failed.");

            return null;
        }
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
