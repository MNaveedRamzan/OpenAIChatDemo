using OpenAI.Chat;
using System.Text;

// API key environment variable se uthayein
string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable set nahi hai.");

// Client banayein
ChatClient client = new(model: "gpt-4o-mini", apiKey: apiKey);

// Conversation history
List<ChatMessage> messages = new()
{
    new SystemChatMessage("Aap ek helpful assistant hain jo concise jawab dete hain.")
};

Console.WriteLine("Chat shuru hai (streaming mode). Exit karne ke liye 'exit' likhein.\n");

while (true)
{
    Console.Write("Aap: ");
    string? userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput)) continue;
    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    // User message history mein add karein
    messages.Add(new UserChatMessage(userInput));

    try
    {
        Console.Write("\nAssistant: ");

        // Streaming API call — chunks aayenge
        var streamingResponse = client.CompleteChatStreamingAsync(messages);

        // Full response build karne ke liye (history mein store karne ke liye)
        StringBuilder fullResponse = new();

        // Token tracking — usage info last chunk mein aati hai
        int inputTokens = 0;
        int outputTokens = 0;

        // Async iteration — har chunk pe loop chalega
        await foreach (var update in streamingResponse)
        {
            // Content delta — actual text jo generate hua iss chunk mein
            foreach (var part in update.ContentUpdate)
            {
                Console.Write(part.Text);          // Live print to console
                fullResponse.Append(part.Text);    // Aggregate for history
            }

            // Usage info (typically last update mein hoti hai)
            if (update.Usage != null)
            {
                inputTokens = update.Usage.InputTokenCount;
                outputTokens = update.Usage.OutputTokenCount;
            }
        }

        Console.WriteLine(); // Streaming ke baad newline

        // History mein assistant ka complete response add karein
        messages.Add(new AssistantChatMessage(fullResponse.ToString()));

        // Token cost tracking
        Console.WriteLine($"[Tokens — Input: {inputTokens}, Output: {outputTokens}]\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}\n");
    }
}

Console.WriteLine("Chat khatam.");