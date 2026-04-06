using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Marker component for the security borg chassis.
/// Used for routing chassis-intrinsic action events.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SecurityBorgComponent : Component { }

public sealed partial class BorgLawbookActionEvent : InstantActionEvent { }
public sealed partial class BorgCallForHelpActionEvent : InstantActionEvent { }
