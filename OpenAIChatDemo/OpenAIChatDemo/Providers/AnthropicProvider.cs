using Anthropic;
using Anthropic.Models.Messages;
using OpenAIChatDemo.Configuration;
using Serilog;

namespace OpenAIChatDemo.Providers;

public class AnthropicProvider : IChatProvider
{
    private readonly AnthropicClient _client;
    private readonly AnthropicSettings _settings;

    public string Name => "Anthropic";
    public string ModelName => _settings.Model;

    public AnthropicProvider(AnthropicSettings settings, string apiKey)
    {
        _settings = settings;
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ChatResponse> SendMessageAsync(List<ChatTurn> conversation)
    {

        // Anthropic separates system prompt from message history
        // Fallback to default if no system message provided (defensive)
        string systemPrompt = conversation
            .FirstOrDefault(t => t.Role == ChatRole.System)?.Content
            ?? "You are a helpful assistant.";

        // Build messages list first (Messages property is IReadOnlyList — cannot mutate after)
        var messagesList = new List<MessageParam>();
        foreach (var turn in conversation.Where(t => t.Role != ChatRole.System))
        {
            messagesList.Add(new MessageParam
            {
                Role = turn.Role == ChatRole.User ? Role.User : Role.Assistant,
                Content = turn.Content
            });
        }

        // All init-only and required members set in initializer
        var parameters = new MessageCreateParams
        {
            Model = _settings.Model,
            MaxTokens = _settings.MaxTokens,
            Messages = messagesList,
            System = systemPrompt
        };

        Log.Debug("Anthropic request: Model: {Model}, MaxTokens: {MaxTokens}, MessageCount: {Count}",
            _settings.Model, _settings.MaxTokens, messagesList.Count);

        var response = await _client.Messages.Create(parameters);

        // Extract text from content blocks (Anthropic returns array of typed blocks)
        string responseText = "";
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
            {
                responseText += textBlock.Text;
            }
        }

        // Cast long → int (Anthropic returns long for future-proofing large contexts)
        return new ChatResponse(
            Content: responseText,
            InputTokens: (int)response.Usage.InputTokens,
            OutputTokens: (int)response.Usage.OutputTokens);
    }
}