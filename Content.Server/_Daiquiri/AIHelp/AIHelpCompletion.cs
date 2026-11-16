using System.Text.Json.Serialization;

namespace Content.Server._Daiquiri.AIHelp;

public enum AIHelpCompletionStatus : byte
{
    Ok,
    NotConfigured,
    AuthError,
    Timeout,
    ApiError,
    Empty,
}

public readonly record struct AIHelpCompletion(AIHelpCompletionStatus Status, string Text, int Tokens, string? Model)
{
    public bool Success => Status == AIHelpCompletionStatus.Ok;

    public static AIHelpCompletion Fail(AIHelpCompletionStatus status)
        => new(status, string.Empty, 0, null);
}

public sealed class AIHelpChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
