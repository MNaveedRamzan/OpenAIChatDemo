using OpenAI.Chat;
using OpenAIChatDemo.Configuration;
using Serilog;

namespace OpenAIChatDemo.Providers;

public class OpenAIProvider : IChatProvider
{
    private readonly ChatClient _client;
    private readonly OpenAISettings _settings;

    public string Name => "OpenAI";
    public string ModelName => _settings.Model;

    public OpenAIProvider(OpenAISettings settings, string apiKey)
    {
        _settings = settings;
        _client = new ChatClient(model: settings.Model, apiKey: apiKey);
    }

    public async Task<ChatResponse> SendMessageAsync(List<ChatTurn> conversation)
    {
        // Convert generic ChatTurn -> OpenAI-specific ChatMessage
        var messages = conversation.Select(turn => (ChatMessage)(turn.Role switch
        {
            ChatRole.System => new SystemChatMessage(turn.Content),
            ChatRole.User => new UserChatMessage(turn.Content),
            ChatRole.Assistant => new AssistantChatMessage(turn.Content),
            _ => throw new ArgumentException($"Unknown role: {turn.Role}")
        })).ToList();

        Log.Debug("OpenAI request: {MessageCount} messages, Model: {Model}",
            messages.Count, _settings.Model);

        ChatCompletion completion = await _client.CompleteChatAsync(messages);

        return new ChatResponse(
            Content: completion.Content[0].Text,
            InputTokens: completion.Usage.InputTokenCount,
            OutputTokens: completion.Usage.OutputTokenCount);
    }
}