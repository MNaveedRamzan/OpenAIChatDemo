namespace OpenAIChatDemo.Providers;

/// <summary>
/// Common interface for AI chat providers (OpenAI, Anthropic, etc.).
/// Enables provider-agnostic chat logic in the application layer.
/// </summary>
public interface IChatProvider
{
    /// <summary>
    /// Provider identifier for logging and display purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Model identifier being used by this provider.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Sends the conversation history and returns the assistant's reply.
    /// </summary>
    /// <param name="conversation">Full conversation history including system prompt.</param>
    /// <returns>Response containing assistant text and token usage.</returns>
    Task<ChatResponse> SendMessageAsync(List<ChatTurn> conversation);
}

/// <summary>
/// Provider-agnostic message role.
/// </summary>
public enum ChatRole
{
    System,
    User,
    Assistant
}

/// <summary>
/// Single turn in a conversation — role + text content.
/// </summary>
public record ChatTurn(ChatRole Role, string Content);

/// <summary>
/// Response from any chat provider.
/// </summary>
public record ChatResponse(
    string Content,
    int InputTokens,
    int OutputTokens);