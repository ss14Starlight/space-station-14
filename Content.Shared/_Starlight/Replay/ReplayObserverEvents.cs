using Content.Shared.Actions;

namespace Content.Shared._Starlight.Replay;

/// <summary>
/// Client-only; in Shared because the server also loads action prototypes.
/// </summary>
public sealed partial class ReplayTogglePlayerOverlayActionEvent : InstantActionEvent { }

/// <summary>
/// Client-only; in Shared because the server also loads action prototypes.
/// </summary>
public sealed partial class ReplayToggleStatusIconsActionEvent : InstantActionEvent { }
