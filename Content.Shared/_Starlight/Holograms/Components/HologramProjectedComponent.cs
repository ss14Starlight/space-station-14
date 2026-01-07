using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.Holograms.Components;

/// <summary>
///     Marks that this Hologram is projected from cameras, or some other hologram projector source.
/// </summary>
[RegisterComponent]
[NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HologramProjectedComponent : Component
{
    /// <summary>
    ///     A whitelist to check for on projectors, to determine if they're valid.
    /// </summary>
    [DataField("validProjectorWhitelist"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public EntityWhitelist ValidProjectorWhitelist = new();

    /// <summary>
    ///     A timer for a grace period before the Holo is returned, to allow for moving through doors.
    /// </summary>
    [DataField("gracePeriod"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(0.1f);

    /// <summary>
    ///     The prototype of the effect to spawn for the Hologram's projection. Leave null to disable the visual projection effect.
    /// </summary>
    [DataField("effectPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    [AutoNetworkedField]
    public string? EffectPrototype;

    /// <summary>
    ///     Whether or not the Hologram's vision should snap to the projector they're projected from.
    /// </summary>
    /// <remarks>
    ///     This provides a super cool effect of the Hologram only getting the visual information they technically should, but it's also a bit of a pain from a player perspective.
    ///     Primarily used for the station AI.
    /// </remarks>
    [DataField("setEyeTarget"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool SetEyeTarget = false;

    /// <summary>
    ///     The current projector the hologram is connected to.
    /// </summary>
    /// <remarks>
    ///     Note that this may not be a valid projector, as it is left set to the last projector the Hologram was in range of during the grace period.
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public NetEntity? CurProjector;

    /// <summary>
    ///     If set, the Hologram will only be able to be projected from this projector, simply ignoring all others.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public NetEntity? ProjectorOverride;

    /// <summary>
    ///     Whether or not the Hologram is currently in the range of a projector.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField] // TODO: Probably remove this and just sync the projector then determine this client side?
    public bool CurrentlyInProjector = false;

    /// <summary>
    ///     The point at which a Hologram will be sent back to their last projector or killed, based on when they were last in the range of one.
    /// </summary>
    /// <remarks>
    ///     Note that THIS WILL NOT BE SET TO NULL. If a hologram enters a projector, this value will be left alone and simply be innacurate.
    ///     Do not rely on it.
    /// </remarks>
    // [AutoNetworkedField]
    public TimeSpan VanishTime = TimeSpan.Zero;

    /// <summary>
    ///     The UID of the entity for the Hologram's visual projection effect.
    ///     Client side only.
    /// </summary>
    public EntityUid? EffectEntity = null;
}
