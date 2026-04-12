namespace Api.Configuration;

// ReSharper disable once InconsistentNaming
public class OpenAIConfiguration
{
    /// <summary>
    ///     API key for the configured <see cref="BaseUrl" /> (OpenRouter key when using OpenRouter).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    ///     OpenAI-compatible chat base URL (include /v1), e.g. https://openrouter.ai/api/v1 or https://api.openai.com/v1.
    /// </summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>
    ///     Model id for the configured provider (e.g. openai/gpt-4o-mini on OpenRouter).
    /// </summary>
    public string Model { get; set; } = "openai/gpt-4o-mini";
}
