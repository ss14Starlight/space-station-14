using Content.Shared._Starlight.Light.Flicker;
using Content.Shared._Starlight.Zones;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Starlight.Light.Flicker;

/// <summary>
/// Makes lights in a station zone, maintenance by default, unreliable: some flicker for the whole round,
/// and every so often another one starts failing for a while and may burn out.
/// </summary>
[RegisterComponent, Access(typeof(SickLightsVariationPassSystem))]
public sealed partial class SickLightsVariationPassComponent : Component
{
    /// <summary>
    /// Determines in which zone we should replace lights with sick variant.
    /// </summary>
    [DataField]
    public ProtoId<ZonePrototype> Zone = "Maintenance";

    /// <summary>
    /// Flicker of a failing light.
    /// </summary>
    [DataField]
    public ProtoId<LightFlickerPrototype> Flicker = "Sick";

    /// <summary>
    /// Chance for a light in the zone to flicker for the whole round.
    /// </summary>
    [DataField]
    public float SickChance = 0.08f;

    /// <summary>
    /// Minimal time for fail delay.
    /// </summary>
    [DataField]
    public TimeSpan MinDelay = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Max time for fail delay.
    /// </summary>
    [DataField]
    public TimeSpan MaxDelay = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Shortest time a light fails for before it settles or burns out.
    /// </summary>
    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Max time a light fails for before it settles or burns out.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Chance for a failing light to burn out at the end, instead of settling.
    /// </summary>
    [DataField]
    public float BurnoutChance = 0.35f;

    /// <summary>
    /// Sound which will be played on burnout
    /// </summary>
    [DataField]
    public SoundSpecifier BurnoutSound = new SoundCollectionSpecifier("sparks");

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextFailure;

    /// <summary>
    /// The station this pass was run for.
    /// </summary>
    [DataField]
    public EntityUid? Station;

    /// <summary>
    /// Lights failing right now, with when they stop.
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, TimeSpan> Failing = new();
}
