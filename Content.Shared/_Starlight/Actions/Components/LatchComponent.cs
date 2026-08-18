using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Components;

/// <summary>
/// Grants a targeted latch ability: immobilizes user and target for a base
/// duration, extendable by biting up to a hard cap.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LatchComponent : Component
{
    /// <summary>
    /// The action this component grants for latching onto a target.
    /// Used for action grants, in the event the ActionEntity is null or invalid.
    /// </summary>
    [DataField]
    public EntProtoId Action = "Latch";

    /// <summary>
    /// The action this component grants for 'biting harder' to extend the latch.
    /// Used for action grants, in the event the BiteHarderActionEntity is null or invalid.
    /// </summary>
    [DataField]
    public EntProtoId BiteHarderAction = "LatchBiteHarder";

    /// <summary>
    /// The action this component grants for releasing the latch early.
    /// Used for action grants, in the event the ReleaseActionEntity is null or invalid.
    /// </summary>
    [DataField]
    public EntProtoId ReleaseAction = "LatchRelease";

    /// <summary>
    /// The alert to display to the target of the latch.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> LatchAlert = "Latched";

    /// <summary>
    /// Shown on the latcher (K9) instead of <see cref="LatchAlert"/>, which is shown on the target.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> LatcherAlert = "K9Latched";

    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Distance a latch breaks at if exceeded mid-latch. Independent of the
    /// action's own engage range (TargetAction.range on the Latch prototype).
    /// </summary>
    [DataField]
    public float DriftBreakRange = 1.5f;

    /// <summary>
    /// Extra slack for the drift check, separate from engage range.
    /// </summary>
    [DataField]
    public float DriftBreakTolerance = 0.5f;

    /// <summary>
    /// The starting, base duration of the latch, in seconds.
    /// </summary>
    [DataField]
    public TimeSpan BaseDuration = TimeSpan.FromSeconds(8);

    /// <summary>
    /// The overall limit to how long the latch can be held for.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long, in seconds, the 'bite harder' action will extend the latch for.
    /// This value is affected by armor values on the latch target.
    /// </summary>
    [DataField]
    public TimeSpan ExtensionPerBite = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long, in seconds, that a hit done to the Latcher will reduce the latch duration.
    /// This value is affected by armor values on the Latcher.
    /// </summary>
    [DataField]
    public TimeSpan ReductionPerHit = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Damage that produces exactly ReductionPerHit/ExtensionPerBite; scales
    /// linearly from there.
    /// </summary>
    [DataField]
    public FixedPoint2 ReferenceDamage = FixedPoint2.New(5);

    /// <summary>
    /// Stamina damage dealt to the target per Bite Harder use.
    /// </summary>
    [DataField]
    public float StaminaDamagePerBite = 15f;

    /// <summary>
    /// How frequently the latch should apply 'ticks', mostly used
    /// for ticking damage onto the latch target.
    /// </summary>
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(0.75);

    /// <summary>
    /// A damage specifier for how much damage will be dealt to a target
    /// on each tick processed for the latch's duration.
    /// </summary>
    [DataField]
    public DamageSpecifier DamagePerTick = new();

    /// <summary>
    /// Chance per damage tick that the target screams and the latcher snarls.
    /// </summary>
    [DataField]
    public float ScreamChance = 0.5f;

    /// <summary>
    /// Played on the latcher when a latch begins. Unset by default.
    /// </summary>
    [DataField]
    public SoundSpecifier? LatchStartSound;

    /// <summary>
    /// Played on the latcher when Bite Harder is used. Unset by default.
    /// </summary>
    [DataField]
    public SoundSpecifier? BiteHarderSound;

    /// <summary>
    /// Whether the latch is currently active.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// The entity being targeted by the latch.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// The specific, discrete end time designated for the latch.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>
    /// Latch start time; ending within RefundGracePeriod of this refunds the charge.
    /// </summary>
    [ViewVariables]
    public TimeSpan StartTime;

    [DataField]
    public TimeSpan RefundGracePeriod = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Fixed hard-cap timestamp set once when the latch starts. Bite Harder
    /// can extend EndTime toward this but never past it.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan MaxEndTime;

    /// <summary>
    /// The timestamp for when the next latch bite tick should occur.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextTickTime;

    /// <summary>
    /// The actual latch action entity to grant to the latcher.
    /// If null or invalid, the Action field is granted instead.
    /// </summary>
    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>
    /// The actual bite harder action entity to grant to the latcher.
    /// If null or invalid, the BiteHarderAction field is granted instead.
    /// </summary>
    [ViewVariables]
    public EntityUid? BiteHarderActionEntity;

    /// <summary>
    /// The actual release action entity to grant to the latcher.
    /// If null or invalid, the ReleaseAction field is granted instead.
    /// </summary>
    [ViewVariables]
    public EntityUid? ReleaseActionEntity;

    /// <summary>
    /// Whether the target's DoT is paused (incapacitated). Movement lock stays active.
    /// </summary>
    [ViewVariables]
    public bool TickPaused;
}
