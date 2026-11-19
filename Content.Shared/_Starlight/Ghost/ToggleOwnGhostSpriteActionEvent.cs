using Content.Shared.Actions;

namespace Content.Shared._Starlight.Ghost;

/// <summary>
/// Client-only; in Shared because the server also loads action prototypes.
/// </summary>
public sealed partial class ToggleOwnGhostSpriteActionEvent : InstantActionEvent { }
