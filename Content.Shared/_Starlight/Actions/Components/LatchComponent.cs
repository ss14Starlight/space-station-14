using Content.Shared.Alert;
using Content.Shared.Damage;
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
    [DataField] public EntProtoId Action = "Latch";
    [DataField] public EntProtoId BiteHarderAction = "LatchBiteHarder";
    [DataField] public EntProtoId ReleaseAction = "LatchRelease";
    [DataField] public ProtoId<AlertPrototype> LatchAlert = "Latched";

    [DataField] public EntityWhitelist? Whitelist;

    [DataField] public float Range = 1.5f;

    [DataField] public TimeSpan BaseDuration = TimeSpan.FromSeconds(8);
    [DataField, AutoNetworkedField] public TimeSpan MaxDuration = TimeSpan.FromSeconds(15);
    [DataField] public TimeSpan ExtensionPerBite = TimeSpan.FromSeconds(2);
    [DataField] public TimeSpan ReductionPerHit = TimeSpan.FromSeconds(1);

    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(0.75);
    [DataField] public DamageSpecifier DamagePerTick = new();

    /// <summary>
    /// Chance per damage tick that the target screams and the latcher snarls.
    /// </summary>
    [DataField] public float ScreamChance = 0.5f;

    /// <summary>
    /// Played on the latcher when a latch begins. Unset by default.
    /// </summary>
    [DataField] public SoundSpecifier? LatchStartSound;

    /// <summary>
    /// Played on the latcher when Bite Harder is used. Unset by default.
    /// </summary>
    [DataField] public SoundSpecifier? BiteHarderSound;

    [ViewVariables, AutoNetworkedField] public bool Active;
    [ViewVariables, AutoNetworkedField] public EntityUid? Target;
    [ViewVariables, AutoNetworkedField] public TimeSpan EndTime;

    /// <summary>
    /// Fixed hard-cap timestamp set once when the latch starts. Bite Harder
    /// can extend EndTime toward this but never past it.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public TimeSpan MaxEndTime;

    [ViewVariables] public TimeSpan NextTickTime;

    [ViewVariables] public EntityUid? ActionEntity;
    [ViewVariables] public EntityUid? BiteHarderActionEntity;
    [ViewVariables] public EntityUid? ReleaseActionEntity;

    /// <summary>
    /// Whether the target's DoT is paused (incapacitated). Movement lock stays active.
    /// </summary>
    [ViewVariables] public bool TickPaused;
}
