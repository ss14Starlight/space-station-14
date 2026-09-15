using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Blocks bare-handed prying on a tutorial door.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialToolOnlyPryComponent : Component;
