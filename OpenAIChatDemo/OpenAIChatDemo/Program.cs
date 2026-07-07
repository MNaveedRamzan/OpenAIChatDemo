using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using OpenAIChatDemo.Configuration;
using Serilog;
using System.ClientModel;
using System.Text;

// Build configuration from appsettings.json
IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

// Configure Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Application starting");

    // Load API key from environment variable
    string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

    // Bind to strongly-typed settings
    AppSettings settings = new();
    configuration.Bind(settings);

    Log.Information("Configuration loaded. Model: {Model}, MaxRetries: {MaxRetries}",
        settings.OpenAI.Model, settings.Retry.MaxAttempts);

    // Create OpenAI chat client
    ChatClient client = new(model: settings.OpenAI.Model, apiKey: apiKey);

    // Conversation history with system prompt
    List<ChatMessage> messages = new()
    {
        new SystemChatMessage(settings.OpenAI.SystemPrompt)
    };

    Console.WriteLine($"\nChat started — Model: {settings.OpenAI.Model} | Type 'exit' to quit.\n");

    while (true)
    {
        Console.Write("You: ");
        string? userInput = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(userInput)) continue;

        if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("User requested exit");
            break;
        }

        messages.Add(new UserChatMessage(userInput));
        Log.Debug("User message added. Total messages in context: {Count}", messages.Count);

        bool success = await StreamWithRetryAsync(client, messages, settings.Retry);

        if (!success)
        {
            messages.RemoveAt(messages.Count - 1);
            Log.Debug("Orphan user message removed from history");
        }
    }

    Console.WriteLine("Chat ended.");
    Log.Information("Application shutting down normally");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    // Ensure all logs are flushed before exit
    Log.CloseAndFlush();
}


// ============================================================
// Streaming + Retry helper
// ============================================================
static async Task<bool> StreamWithRetryAsync(
    ChatClient client,
    List<ChatMessage> messages,
    RetrySettings retrySettings)
{
    int delayMs = retrySettings.InitialDelayMs;

    for (int attempt = 1; attempt <= retrySettings.MaxAttempts; attempt++)
    {
        try
        {
            Console.Write("\nAssistant: ");

            StringBuilder fullResponse = new();
            int inputTokens = 0, outputTokens = 0;

            await foreach (var update in client.CompleteChatStreamingAsync(messages))
            {
                foreach (var part in update.ContentUpdate)
                {
                    Console.Write(part.Text);
                    fullResponse.Append(part.Text);
                }

                if (update.Usage != null)
                {
                    inputTokens = update.Usage.InputTokenCount;
                    outputTokens = update.Usage.OutputTokenCount;
                }
            }

            Console.WriteLine();
            messages.Add(new AssistantChatMessage(fullResponse.ToString()));
            Console.WriteLine($"[Tokens — Input: {inputTokens}, Output: {outputTokens}]\n");

            Log.Information(
                "Chat completion successful. InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, Attempt: {Attempt}",
                inputTokens, outputTokens, attempt);

            return true;
        }

        // ========= TRANSIENT errors — retry with backoff =========
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            Log.Warning(
                "Rate limit hit (429). Attempt: {Attempt}/{MaxAttempts}, RetryDelay: {DelayMs}ms",
                attempt, retrySettings.MaxAttempts, delayMs);
            Console.WriteLine($"\n⏳ Rate limit hit. Retrying...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            Log.Warning(
                "Server error ({Status}). Attempt: {Attempt}/{MaxAttempts}, RetryDelay: {DelayMs}ms",
                ex.Status, attempt, retrySettings.MaxAttempts, delayMs);
            Console.WriteLine($"\n⚠️ Server error. Retrying...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex,
                "Network error. Attempt: {Attempt}/{MaxAttempts}, RetryDelay: {DelayMs}ms",
                attempt, retrySettings.MaxAttempts, delayMs);
            Console.WriteLine($"\n🌐 Network error. Retrying...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }
        catch (TaskCanceledException ex)
        {
            Log.Warning(ex,
                "Request timeout. Attempt: {Attempt}/{MaxAttempts}, RetryDelay: {DelayMs}ms",
                attempt, retrySettings.MaxAttempts, delayMs);
            Console.WriteLine($"\n⏱️ Timeout. Retrying...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }

        // ========= PERMANENT errors — fail fast =========
        catch (ClientResultException ex) when (ex.Status == 401)
        {
            Log.Error("Authentication failed (401). API key may be invalid.");
            Console.WriteLine("\n❌ Authentication failed. Check your OPENAI_API_KEY.");
            return false;
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            Log.Error(ex, "Invalid request (400): {Message}", ex.Message);
            Console.WriteLine($"\n❌ Invalid request: {ex.Message}");
            return false;
        }
        catch (ClientResultException ex)
        {
            Log.Error(ex, "API error ({Status}): {Message}", ex.Status, ex.Message);
            Console.WriteLine($"\n❌ API error ({ex.Status}): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during chat completion");
            Console.WriteLine($"\n❌ Unexpected error: {ex.Message}");
            return false;
        }
    }

    Log.Error("Max retries ({MaxAttempts}) exhausted", retrySettings.MaxAttempts);
    Console.WriteLine($"\n❌ Max retries exhausted. Skipping this request.");
    return false;
}
