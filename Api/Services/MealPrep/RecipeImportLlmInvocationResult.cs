namespace Api.Services.MealPrep;

/// <summary>
///     Outcome of a recipe-import LLM call, including payloads suitable for persistence.
/// </summary>
internal sealed record RecipeImportLlmInvocationResult(
    RecipeImportLlmStructuredDto? Structured,
    string ProviderBaseUrl,
    string Model,
    string RequestJson,
    string? ResponseContent,
    string? FinishReason,
    bool ParsedSuccessfully,
    string? FailureDetail
);
