// ReSharper disable once CheckNamespace
namespace Content.Server.Chat.Systems;

/// <summary>Allows content to handle or redirect an IC message before normal speech processing.</summary>
public sealed class BeforeInGameICMessageEvent(EntityUid source, string message) : EntityEventArgs
{
    public EntityUid Source = source;
    public readonly string Message = message;
    public bool Handled;
}
