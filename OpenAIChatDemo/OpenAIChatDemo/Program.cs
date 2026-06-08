using OpenAI.Chat;

// API key environment variable se uthayein
string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable set nahi hai.");

// Client banayein
ChatClient client = new(model: "gpt-4o-mini", apiKey: apiKey);

// Conversation history maintain karne ke liye list
List<ChatMessage> messages = new()
{
    new SystemChatMessage("Aap ek helpful assistant hain jo concise jawab dete hain.")
};

Console.WriteLine("Chat shuru hai. Exit karne ke liye 'exit' likhein.\n");

while (true)
{
    Console.Write("Aap: ");
    string? userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput)) continue;
    if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    // User message add karein
    messages.Add(new UserChatMessage(userInput));

    try
    {
        // API call
        ChatCompletion completion = await client.CompleteChatAsync(messages);

        string assistantReply = completion.Content[0].Text;

        // History mein assistant ka response bhi add karein (context ke liye)
        messages.Add(new AssistantChatMessage(assistantReply));

        Console.WriteLine($"\nAssistant: {assistantReply}\n");

        // Token usage dekhne ke liye (cost tracking)
        Console.WriteLine($"[Tokens — Input: {completion.Usage.InputTokenCount}, Output: {completion.Usage.OutputTokenCount}]\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}\n");
    }
}

Console.WriteLine("Chat khatam.");