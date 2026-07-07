using Microsoft.Extensions.Configuration;
using OpenAIChatDemo.Configuration;
using OpenAIChatDemo.Providers;
using Serilog;

// Build configuration
IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Application starting");

    // Load settings
    AppSettings settings = new();
    configuration.Bind(settings);

    // Select provider based on config
    IChatProvider provider = CreateProvider(settings);

    Log.Information("Provider initialized: {Provider}, Model: {Model}",
        provider.Name, provider.ModelName);

    // Get system prompt from the active provider's settings
    string systemPrompt = settings.AIProvider.Active.ToLower() == "anthropic"
        ? settings.Anthropic.SystemPrompt
        : settings.OpenAI.SystemPrompt;

    List<ChatTurn> conversation = new()
    {
        new ChatTurn(ChatRole.System, systemPrompt)
    };

    Console.WriteLine($"\n╔════════════════════════════════════════════════╗");
    Console.WriteLine($"║  Multi-Model Chat Demo                         ║");
    Console.WriteLine($"║  Provider: {provider.Name,-36}║");
    Console.WriteLine($"║  Model:    {provider.ModelName,-36}║");
    Console.WriteLine($"╚════════════════════════════════════════════════╝");
    Console.WriteLine("Commands: 'exit' to quit, 'switch' to change provider\n");

    while (true)
    {
        Console.Write("You: ");
        string? userInput = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(userInput)) continue;

        // Exit command
        if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("User requested exit");
            break;
        }

        // Switch provider command
        if (userInput.Trim().Equals("switch", StringComparison.OrdinalIgnoreCase))
        {
            settings.AIProvider.Active = settings.AIProvider.Active == "openai"
                ? "anthropic"
                : "openai";
            provider = CreateProvider(settings);

            // Reset system prompt for new provider
            string newSystemPrompt = settings.AIProvider.Active == "anthropic"
                ? settings.Anthropic.SystemPrompt
                : settings.OpenAI.SystemPrompt;

            conversation = new List<ChatTurn>
            {
                new ChatTurn(ChatRole.System, newSystemPrompt)
            };

            Console.WriteLine($"\n>>> Switched to {provider.Name} ({provider.ModelName}). History cleared.\n");
            Log.Information("Provider switched to: {Provider}", provider.Name);
            continue;
        }

        conversation.Add(new ChatTurn(ChatRole.User, userInput));

        try
        {
            Console.Write($"\n{provider.Name}: ");

            var response = await provider.SendMessageAsync(conversation);

            Console.WriteLine(response.Content);
            Console.WriteLine($"[Tokens — Input: {response.InputTokens}, Output: {response.OutputTokens}]\n");

            conversation.Add(new ChatTurn(ChatRole.Assistant, response.Content));

            Log.Information(
                "Chat completion successful. Provider: {Provider}, InputTokens: {Input}, OutputTokens: {Output}",
                provider.Name, response.InputTokens, response.OutputTokens);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Chat request failed for provider: {Provider}", provider.Name);
            Console.WriteLine($"\n❌ Error: {ex.Message}\n");

            // Remove orphan user message
            conversation.RemoveAt(conversation.Count - 1);
        }
    }

    Console.WriteLine("Chat ended.");
    Log.Information("Application shutting down");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


// ============================================================
// Provider Factory
// ============================================================
static IChatProvider CreateProvider(AppSettings settings)
{
    return settings.AIProvider.Active.ToLower() switch
    {
        "openai" => new OpenAIProvider(
            settings.OpenAI,
            Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("OPENAI_API_KEY not set")),

        "anthropic" => new AnthropicProvider(
            settings.Anthropic,
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set")),

        _ => throw new InvalidOperationException(
            $"Unknown provider: {settings.AIProvider.Active}. Use 'openai' or 'anthropic'.")
    };
}