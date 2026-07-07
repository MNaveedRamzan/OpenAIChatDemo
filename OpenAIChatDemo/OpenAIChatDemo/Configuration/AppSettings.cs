namespace OpenAIChatDemo.Configuration;

public class AppSettings
{
    public AIProviderSettings AIProvider { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
    public AnthropicSettings Anthropic { get; set; } = new();
    public RetrySettings Retry { get; set; } = new();
}

public class AIProviderSettings
{
    public string Active { get; set; } = "openai";
}

public class OpenAISettings
{
    public string Model { get; set; } = "gpt-4o-mini";
    public string SystemPrompt { get; set; } = "You are a helpful assistant.";
}

public class AnthropicSettings
{
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
    public string SystemPrompt { get; set; } = "You are a helpful assistant.";
    public int MaxTokens { get; set; } = 1024;
}

public class RetrySettings
{
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 1000;
    public int BackoffMultiplier { get; set; } = 2;
}