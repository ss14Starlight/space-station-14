using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a practice anomaly so tutorial systems can pin starting severity/stability after MapInit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialAnomalyComponent : Component;
