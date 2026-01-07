namespace Content.Shared._Starlight.Holograms;

/// <summary>
///     Sent directed at a Hologram about to be returned to be handled by other systems.
/// </summary>
/// <remarks>
///     Note that if this event is cancelled, there should be a plan to deal with it, since it'll just happen again next frame.
/// </remarks>
[ByRefEvent]
public record struct HologramReturnAttemptEvent(bool Cancelled = false);

/// <summary>
///     Sent directed at a Hologram before they are returned, often due to not being near a projector.
/// </summary>
/// <param name="Projector">The projector the Hologram is being returned to.</param>
public readonly record struct HologramReturnedEvent(EntityUid Projector);

/// <summary>
///     Sent directed at a Hologram about to be killed to be handled by other systems.
/// </summary>
/// <remarks>
///     Note that if this event is cancelled, there should be a plan to deal with it, since it'll just happen again next frame.
/// </remarks>
[ByRefEvent]
public record struct HologramKillAttemptEvent(bool Cancelled = false);

/// <summary>
///     Sent directed at a Hologram being killed, often due to not having any valid projectors.
/// </summary>
public readonly record struct HologramKilledEvent();

/// <summary>
///     Sent directed at a Hologram when searching for any valid Projectors.
///     Allows for manually setting the projector to use.
///     Note that this Projector will not be validated in *any* way.
/// </summary>
/// <remarks>
///     Setting override to 'True' will use whatever's in ProjectorOverride- including a null value, which allows cancelling the projector search.
///     A Component-set override will override this override.
/// </remarks>
[ByRefEvent]
public record struct HologramGetProjectorEvent(EntityUid? ProjectorOverride = null, bool Override = false);

/// <summary>
///     Sent directed at a Hologram when they're checking if a specific projector is valid.
///     Allows for manually determining if a projector is valid for a given Hologram.
/// </summary>
/// <remarks>
///     Setting Valid to either True or False will force that behavior.
///     Leaving it null will allow the projector to determine its own validity based on normal rules.
/// </remarks>
[ByRefEvent]
public record struct HologramCheckProjectorValidEvent(EntityUid Projector, bool? Valid = null);
