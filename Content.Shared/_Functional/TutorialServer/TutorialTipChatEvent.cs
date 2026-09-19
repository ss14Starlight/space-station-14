using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Server → client tip for the chat box. Markup may include <c>[keybind]…[/keybind]</c>
/// tags resolved on the client against the player's bindings.
/// </summary>
[Serializable, NetSerializable]
public sealed class TutorialTipChatEvent : EntityEventArgs
{
    public string Markup = string.Empty;
}
