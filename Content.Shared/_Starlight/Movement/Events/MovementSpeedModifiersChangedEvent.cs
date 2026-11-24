namespace Content.Shared._Starlight.Movement.Events;

/// <summary>
/// Raised after the walk and sprint speed modifiers are stored.
/// Vehicles use this event to apply the driver's new modifiers in the same tick.
/// </summary>
[ByRefEvent]
public readonly record struct MovementSpeedModifiersChangedEvent;
