using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using OpenAIChatDemo.Configuration;
using System.ClientModel;
using System.Text;

// Load API key from environment variable
string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

// Build configuration from appsettings.json
IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

// Bind to strongly-typed settings object
AppSettings settings = new();
configuration.Bind(settings);

// Create OpenAI chat client using configured model
ChatClient client = new(model: settings.OpenAI.Model, apiKey: apiKey);

// Conversation history with configured system prompt
List<ChatMessage> messages = new()
{
    new SystemChatMessage(settings.OpenAI.SystemPrompt)
};

Console.WriteLine($"Chat started — Model: {settings.OpenAI.Model} | Type 'exit' to quit.\n");

while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput)) continue;
    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    messages.Add(new UserChatMessage(userInput));

    bool success = await StreamWithRetryAsync(client, messages, settings.Retry);

    if (!success)
    {
        messages.RemoveAt(messages.Count - 1);
    }
}

Console.WriteLine("Chat ended.");


// ============================================================
// Streaming + Retry helper (now uses injected RetrySettings)
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
            return true;
        }

        // ========= TRANSIENT errors — retry with backoff =========
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            Console.WriteLine($"\n⏳ Rate limit hit. Retrying {attempt}/{retrySettings.MaxAttempts} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            Console.WriteLine($"\n⚠️ Server error ({ex.Status}). Retrying {attempt}/{retrySettings.MaxAttempts} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"\n🌐 Network error. Retrying {attempt}/{retrySettings.MaxAttempts} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"\n⏱️ Timeout. Retrying {attempt}/{retrySettings.MaxAttempts} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= retrySettings.BackoffMultiplier;
        }

        // ========= PERMANENT errors — fail fast =========
        catch (ClientResultException ex) when (ex.Status == 401)
        {
            Console.WriteLine("\n❌ Authentication failed. Check your OPENAI_API_KEY.");
            return false;
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            Console.WriteLine($"\n❌ Invalid request: {ex.Message}");
            return false;
        }
        catch (ClientResultException ex)
        {
            Console.WriteLine($"\n❌ API error ({ex.Status}): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Unexpected error: {ex.Message}");
            return false;
        }
    }

    Console.WriteLine($"\n❌ Max retries ({retrySettings.MaxAttempts}) exhausted. Skipping this request.");
    return false;
}