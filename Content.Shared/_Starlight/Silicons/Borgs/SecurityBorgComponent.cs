using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Marker component for the security borg chassis.
/// Used for routing chassis-intrinsic action events.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SecurityBorgComponent : Component;

/// <summary>
/// Action event raised when a security borg uses their lawbook action. This is used to open the guidebook to the space law page.
/// </summary>
public sealed partial class BorgLawbookActionEvent : InstantActionEvent;

/// <summary>
/// Action event raised when a security borg uses their call for help action. This is used to send a radio message to the security channel with the position of the borg.
/// </summary>
public sealed partial class BorgCallForHelpActionEvent : InstantActionEvent;
