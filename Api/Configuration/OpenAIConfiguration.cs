namespace Api.Configuration;

// ReSharper disable once InconsistentNaming
public class OpenAIConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
}
