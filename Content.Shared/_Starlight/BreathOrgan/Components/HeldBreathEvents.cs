using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.BreathOrgan.Components;

/// <summary>
/// This contains all the events raised by the HeldBreathSystem
/// </summary>

/// <summary>
///     Raised directed on an entity when it is holding it's breath.
/// </summary>
[ByRefEvent]
public record struct HeldBreathEvent(EntityUid Target);

/// <summary>
///     Raised on an entity holding their breath when something wants to remove the held breath component.
/// </summary>
[ByRefEvent]
public record struct HeldBreathEndAttemptEvent(bool Cancelled);

public sealed partial class HeldBreathAlertEvent : BaseAlertEvent;
