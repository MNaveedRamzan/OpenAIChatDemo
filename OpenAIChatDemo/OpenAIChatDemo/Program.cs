using OpenAI.Chat;
using System.ClientModel;
using System.Text;

// Load API key from environment variable
string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

// Create OpenAI chat client
ChatClient client = new(model: "gpt-4o-mini", apiKey: apiKey);

// Conversation history with system prompt
List<ChatMessage> messages = new()
{
    new SystemChatMessage("You are a helpful assistant that provides concise answers.")
};

Console.WriteLine("Chat started (streaming + retry mode). Type 'exit' to quit.\n");

while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput)) continue;
    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    // Add user message to history
    messages.Add(new UserChatMessage(userInput));

    bool success = await StreamWithRetryAsync(client, messages);

    if (!success)
    {
        // Failed call — remove orphan user message from history
        // (otherwise the next call gets confused by a ghost user message)
        messages.RemoveAt(messages.Count - 1);
    }
}

Console.WriteLine("Chat ended.");


// ============================================================
// Streaming + Retry helper
// ============================================================
static async Task<bool> StreamWithRetryAsync(ChatClient client, List<ChatMessage> messages)
{
    const int MaxRetries = 3;
    int delayMs = 1000;

    for (int attempt = 1; attempt <= MaxRetries; attempt++)
    {
        try
        {
            Console.Write("\nAssistant: ");

            // Build full response for history storage
            StringBuilder fullResponse = new();
            int inputTokens = 0, outputTokens = 0;

            // Streaming API call — receives response chunks
            await foreach (var update in client.CompleteChatStreamingAsync(messages))
            {
                foreach (var part in update.ContentUpdate)
                {
                    Console.Write(part.Text);
                    fullResponse.Append(part.Text);
                }

                // Token usage info arrives in the final chunk
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
            Console.WriteLine($"\n⏳ Rate limit hit. Retrying {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;  // Exponential backoff: 1s → 2s → 4s
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            Console.WriteLine($"\n⚠️ Server error ({ex.Status}). Retrying {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"\n🌐 Network error. Retrying {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"\n⏱️ Timeout. Retrying {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;
        }

        // ========= PERMANENT errors — retry is pointless =========
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

    Console.WriteLine($"\n❌ Max retries ({MaxRetries}) exhausted. Skipping this request.");
    return false;
}