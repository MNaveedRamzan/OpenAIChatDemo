namespace OpenAIChatDemo.Configuration;

public class AppSettings
{
    public OpenAISettings OpenAI { get; set; } = new();
    public RetrySettings Retry { get; set; } = new();
}

public class OpenAISettings
{
    public string Model { get; set; } = "gpt-4o-mini";
    public string SystemPrompt { get; set; } = "You are a helpful assistant.";
}

public class RetrySettings
{
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 1000;
    public int BackoffMultiplier { get; set; } = 2;
}