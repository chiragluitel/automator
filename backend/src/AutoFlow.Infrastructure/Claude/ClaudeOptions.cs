namespace AutoFlow.Infrastructure.Claude;

public class ClaudeOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1/messages";
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary>Configurable so the model can be swapped without code changes.</summary>
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 8000;
}
