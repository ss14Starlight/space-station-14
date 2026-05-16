using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Mech;

[Serializable, NetSerializable]
public enum MechVisuals : byte
{
    Open, //whether or not it's open and has a rider
    Broken, //if it broke and no longer works.
    Light, //if lights are enabled
    Siren //if siren are enabled
}

[Serializable, NetSerializable]
public enum MechAssemblyVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum MechVisualLayers : byte
{
    Base,
    Light,
    Siren
}

[Serializable, NetSerializable]
public enum EquipmentType : byte
{
    Active,
    Passive
}

/// <summary>
/// Event raised on equipment when it is inserted into a mech
/// </summary>
[ByRefEvent]
public readonly record struct MechEquipmentInsertedEvent(EntityUid Mech)
{
    public readonly EntityUid Mech = Mech;
}

/// <summary>
/// Event raised on equipment when it is removed from a mech
/// </summary>
[ByRefEvent]
public readonly record struct MechEquipmentRemovedEvent(EntityUid Mech)
{
    public readonly EntityUid Mech = Mech;
}

/// <summary>
/// Raised on the mech equipment before it is going to be removed.
/// </summary>
[ByRefEvent]
public record struct AttemptRemoveMechEquipmentEvent()
{
    public bool Cancelled = false;
}

public sealed partial class MechToggleEquipmentEvent : InstantActionEvent
{
}

public sealed partial class MechOpenUiEvent : InstantActionEvent
{
}

public sealed partial class MechEjectPilotEvent : InstantActionEvent
{
}

#region Starlight
public sealed partial class MechToggleInternalsEvent : InstantActionEvent
{
}

public sealed partial class MechToggleSirensEvent : InstantActionEvent
{
}

public sealed partial class MechToggleThrustersEvent : InstantActionEvent
{
}

public sealed partial class MechToggleNightVisionEvent : InstantActionEvent
{
}

/// <summary>
/// Event raised to honk the air horn. Honk!
/// </summary>
public sealed partial class MechActivateAirHornEvent : InstantActionEvent
{
}

[ByRefEvent]
public readonly record struct BeforePilotEjectEvent(EntityUid Mech, EntityUid Pilot)
{
    public readonly EntityUid Mech = Mech;

    public readonly EntityUid Pilot = Pilot;
}

[ByRefEvent]
public readonly record struct BeforePilotInsertEvent(EntityUid Mech, EntityUid Pilot)
{
    public readonly EntityUid Mech = Mech;

    public readonly EntityUid Pilot = Pilot;
}

/// <summary>
/// Raised on a mech when attempting to get the passive draw rate (units per second)
/// </summary>
/// <param name="mech">The entity this event was raised on</param>
/// <returns name="CumulativeDrawRate">The total draw rate across all active components and equipment</returns>
/// <remarks>
/// Add to the accumulator value for every component that should currently be drawing power/generating heat on the mech
/// </remarks>
public sealed class GetPassiveChargeDrawRate(EntityUid mech)
{
    public readonly EntityUid Mech = mech;
    public float CumulativeDrawRate = 0f;
}
#endregion
