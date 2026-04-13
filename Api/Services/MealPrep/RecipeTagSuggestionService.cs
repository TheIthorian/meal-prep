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
///     Suggests canonical whitelist tags for a recipe draft using the configured chat model.
/// </summary>
public sealed class RecipeTagSuggestionService
{
    private const string SystemPrompt = """
                                        You assign recipe tags for search and filtering. Output only tags that fit the recipe content.
                                        Each tag must be copied exactly from the allowed list provided in the user message.
                                        Prefer 3–8 tags. Omit tags you are unsure about. Do not invent tags outside the list.
                                        """;

    private static readonly BinaryData TagSuggestionJsonSchema = BinaryData.FromString(
        """
        {
          "type": "object",
          "properties": {
            "tags": {
              "type": "array",
              "maxItems": 12,
              "items": { "type": "string" }
            }
          },
          "required": ["tags"],
          "additionalProperties": false
        }
        """
    );

    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ChatClient? _chatClient;
    private readonly ILogger<RecipeTagSuggestionService> _logger;
    private readonly string _model;

    public RecipeTagSuggestionService(
        IOptions<OpenAIConfiguration> options,
        ILogger<RecipeTagSuggestionService> logger
    ) {
        _logger = logger;
        var cfg = options.Value;
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

    public bool IsConfigured => _chatClient is not null;

    /// <summary>
    ///     Returns normalized whitelist tags, or null when the model is not configured or the call fails.
    /// </summary>
    public async Task<string[]?> TrySuggestTagsAsync(
        string title,
        string? description,
        IReadOnlyList<string> ingredientNames,
        IReadOnlyList<string> stepInstructions,
        CancellationToken cancellationToken = default
    ) {
        if (_chatClient is null)
            return null;

        using var scope = _logger.BeginPropertyScope(("recipeTitleLength", title.Length));

        var ingredientsBlock = string.Join(
            "\n",
            ingredientNames.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => $"- {s}")
        );
        var stepsBlock = string.Join(
            "\n",
            stepInstructions
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select((s, i) => $"{i + 1}. {s}")
        );

        var userContent = $"""
                           Allowed tags (copy exactly, use only these strings):
                           {RecipeTagWhitelist.FormatForPrompt()}

                           Title: {title}

                           Description:
                           {description ?? ""}

                           Ingredients:
                           {ingredientsBlock}

                           Steps:
                           {stepsBlock}
                           """;

        var messages = new List<ChatMessage> { new SystemChatMessage(SystemPrompt), new UserChatMessage(userContent) };

        var options = new ChatCompletionOptions {
            Temperature = 0.2f,
            MaxOutputTokenCount = 512,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "recipe_tag_suggest",
                jsonSchema: TagSuggestionJsonSchema,
                jsonSchemaIsStrict: true
            )
        };

        try {
            ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(
                messages,
                options,
                cancellationToken
            );
            var completion = result.Value;

            if (!string.IsNullOrWhiteSpace(completion.Refusal)) {
                _logger.LogWarning("Recipe tag suggestion LLM refused to respond.");

                return null;
            }

            if (completion.Content.Count == 0 || string.IsNullOrWhiteSpace(completion.Content[0].Text)) {
                _logger.LogWarning("Recipe tag suggestion LLM returned empty content.");

                return null;
            }

            var text = completion.Content[0].Text.Trim();
            var dto = JsonSerializer.Deserialize<TagSuggestDto>(text, SerializerOptions);
            if (dto?.Tags is null)
                return null;

            return RecipeTagWhitelist.NormalizeToWhitelist(dto.Tags);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Recipe tag suggestion LLM request failed.");

            return null;
        }
    }

    private sealed class TagSuggestDto
    {
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    }
}
