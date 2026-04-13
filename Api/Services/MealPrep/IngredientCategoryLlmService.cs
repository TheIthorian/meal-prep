using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Configuration;
using Api.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Api.Services.MealPrep;

/// <summary>
///     Calls the configured chat model to assign grocery store categories to ingredient names (batch JSON output).
/// </summary>
public sealed class IngredientCategoryLlmService
{
    private const int MaxNamesPerRequest = 120;
    private const string SystemPrompt = """
        You assign grocery store section categories to ingredient names. Each input line is a normalized ingredient name (lowercase words).
        Use one short category label per ingredient, consistent across similar items. Prefer these when they fit:
        Produce, Dairy & Eggs, Meat & Seafood, Bakery, Pantry & Dry Goods, Frozen, Beverages, Spices & Seasonings,
        Oils & Vinegars, Canned & Jarred, Condiments & Sauces, Other.
        Output JSON only. Every input name must appear exactly once in the items array with the same normalizedIngredientName string.
        """;

    private static readonly BinaryData CategoriesJsonSchema = BinaryData.FromString(
        """
        {
          "type": "object",
          "properties": {
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "normalizedIngredientName": { "type": "string" },
                  "category": { "type": "string" }
                },
                "required": ["normalizedIngredientName", "category"],
                "additionalProperties": false
              }
            }
          },
          "required": ["items"],
          "additionalProperties": false
        }
        """
    );

    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private readonly ChatClient? _chatClient;
    private readonly ILogger<IngredientCategoryLlmService> _logger;
    private readonly string _model;

    public IngredientCategoryLlmService(IOptions<OpenAIConfiguration> options, ILogger<IngredientCategoryLlmService> logger)
    {
        _logger = logger;
        var cfg = options.Value;
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
    ///     Returns categories for the given names, or null when LLM is not configured or the call fails.
    /// </summary>
    internal async Task<IReadOnlyDictionary<string, string>?> TryCategorizeAsync(
        IReadOnlyList<string> normalizedNames,
        CancellationToken cancellationToken = default
    )
    {
        if (_chatClient is null || normalizedNames.Count == 0)
            return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var offset = 0; offset < normalizedNames.Count; offset += MaxNamesPerRequest)
        {
            var chunk = normalizedNames.Skip(offset).Take(MaxNamesPerRequest).ToArray();
            var chunkMap = await TryCategorizeChunkAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (chunkMap is null)
                return null;

            foreach (var pair in chunkMap)
                result[pair.Key] = pair.Value;
        }

        return result;
    }

    private async Task<Dictionary<string, string>?> TryCategorizeChunkAsync(
        IReadOnlyList<string> chunk,
        CancellationToken cancellationToken
    )
    {
        var listText = string.Join("\n", chunk.Select(n => $"- {n}"));

        using var scope = _logger.BeginPropertyScope(("ingredientCount", chunk.Count));

        var messages = new List<ChatMessage> {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(
                $"""
                Assign a grocery category to each ingredient name below. Names must match exactly in your JSON output.

                {listText}
                """
            )
        };

        var options = new ChatCompletionOptions {
            Temperature = 0.1f,
            MaxOutputTokenCount = 4096,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "ingredient_categories",
                jsonSchema: CategoriesJsonSchema,
                jsonSchemaIsStrict: true
            )
        };

        try
        {
            ClientResult<ChatCompletion> completionResult =
                await _chatClient!.CompleteChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
            var completion = completionResult.Value;

            if (!string.IsNullOrWhiteSpace(completion.Refusal))
            {
                _logger.LogWarning("Ingredient category LLM refused to respond.");

                return null;
            }

            if (completion.Content.Count == 0)
            {
                _logger.LogWarning("Ingredient category LLM returned no content.");

                return null;
            }

            var text = completion.Content[0].Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Ingredient category LLM returned empty text.");

                return null;
            }

            var dto = JsonSerializer.Deserialize<IngredientCategoryLlmResponseDto>(text, SerializerOptions);
            if (dto?.Items is null || dto.Items.Count == 0)
            {
                _logger.LogWarning("Ingredient category LLM returned JSON without items.");

                return null;
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in dto.Items)
            {
                if (string.IsNullOrWhiteSpace(row.NormalizedIngredientName) || string.IsNullOrWhiteSpace(row.Category))
                    continue;

                var key = row.NormalizedIngredientName.Trim();
                var value = row.Category.Trim();
                if (value.Length > 128)
                    value = value[..128];

                map[key] = value;
            }

            return map;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ingredient category LLM request failed.");

            return null;
        }
    }

    private sealed class IngredientCategoryLlmResponseDto
    {
        [JsonPropertyName("items")] public List<IngredientCategoryLlmItemDto>? Items { get; set; }
    }

    private sealed class IngredientCategoryLlmItemDto
    {
        [JsonPropertyName("normalizedIngredientName")] public string? NormalizedIngredientName { get; set; }

        [JsonPropertyName("category")] public string? Category { get; set; }
    }
}
