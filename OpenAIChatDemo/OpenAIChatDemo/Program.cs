using OpenAI.Chat;
using System.ClientModel;
using System.Text;

string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable set nahi hai.");

ChatClient client = new(model: "gpt-4o-mini", apiKey: apiKey);

List<ChatMessage> messages = new()
{
    new SystemChatMessage("Aap ek helpful assistant hain jo concise jawab dete hain.")
};

Console.WriteLine("Chat shuru hai (streaming + retry). Exit karne ke liye 'exit' likhein.\n");

while (true)
{
    Console.Write("Aap: ");
    string? userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput)) continue;
    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    messages.Add(new UserChatMessage(userInput));

    bool success = await StreamWithRetryAsync(client, messages);

    if (!success)
    {
        // Failed call — last user message ko history se nikalein
        // (warna agla call ek "ghost" user message ke saath confused ho jayega)
        messages.RemoveAt(messages.Count - 1);
    }
}

Console.WriteLine("Chat khatam.");


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
            return true;  // Success — retry loop se exit
        }

        // ========= TRANSIENT errors — retry karein =========
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            Console.WriteLine($"\n⏳ Rate limit hit. Retry {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;  // Exponential backoff: 1s → 2s → 4s
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            Console.WriteLine($"\n⚠️ Server error ({ex.Status}). Retry {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"\n🌐 Network error. Retry {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"\n⏱️ Timeout. Retry {attempt}/{MaxRetries} after {delayMs}ms...");
            await Task.Delay(delayMs);
            delayMs *= 2;
        }

        // ========= PERMANENT errors — retry waste of time =========
        catch (ClientResultException ex) when (ex.Status == 401)
        {
            Console.WriteLine("\n❌ Authentication failed. OPENAI_API_KEY check karein.");
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

    Console.WriteLine($"\n❌ Max retries ({MaxRetries}) exhausted. Request skip kar rahe hain.");
    return false;
}