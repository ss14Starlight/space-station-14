using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Marks a station entity as a tutorial cargo station (QM approve fulfill stub, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialCargoStationComponent : Component;
