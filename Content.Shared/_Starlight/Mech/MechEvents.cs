using Content.Shared.Actions;

namespace Content.Shared._Starlight.Mech;

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
