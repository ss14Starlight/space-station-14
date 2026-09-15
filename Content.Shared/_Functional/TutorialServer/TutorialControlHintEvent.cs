using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Server -> client control hint for the on-screen banner. Markup may include
/// <c>[keybind="MoveUp"]</c> tags, which resolve against the player's own bindings when the client renders them.
/// </summary>
[Serializable, NetSerializable]
public sealed class TutorialControlHintEvent : EntityEventArgs
{
    /// <summary>
    /// Markup to display. Ignored when <see cref="Show"/> is false.
    /// </summary>
    public string Markup = string.Empty;

    /// <summary>
    /// False hides the banner (the current sub-goal has no control to teach).
    /// </summary>
    public bool Show;
}
