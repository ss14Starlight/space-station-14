using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// A staged effect that fires when the player reaches a named sub-goal, so a curriculum can put
/// something in the world on cue rather than only asking the player for things.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialCueComponent : Component
{
    /// <summary>Sub-goal id that sets this off when it becomes current.</summary>
    [DataField(required: true)]
    public string SubGoalId = string.Empty;

    /// <summary>
    /// Fire this long after the sub-goal starts. With <see cref="AfterLine"/> set it is only the
    /// backstop for a beat the coach never speaks: while she is working toward that line it is
    /// pushed back, so it can never go off partway through what it was meant to punctuate.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    /// <summary>
    /// Fire once the coach has spoken this many lines of <see cref="SubGoalId"/> (one-based), so
    /// rewording her script moves the effect with it instead of stranding it on the wrong line.
    /// </summary>
    [DataField]
    public int? AfterLine;

    /// <summary>Beat between <see cref="AfterLine"/> and the effect; small, so they read as one moment.</summary>
    [DataField]
    public TimeSpan LineDelay = TimeSpan.Zero;

    [DataField]
    public TutorialCueEffect Effect = TutorialCueEffect.Breach;

    /// <summary>Radius in tiles for the lighting effects. Sized to one chamber, not through a wall.</summary>
    [DataField]
    public float Radius = 8f;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>Cosmetic entity spawned where the cue fires.</summary>
    [DataField]
    public EntProtoId? Spawn;

    /// <summary>
    /// Sends whatever <see cref="Spawn"/> just put in the world walking at the nearest entity
    /// carrying this tag. For staging somebody who has come here for somebody: a bystander who
    /// arrives on cue and then stands where they spawned is a prop, not a scene.
    /// </summary>
    [DataField]
    public string? SpawnFollowTag;

    /// <summary>
    /// Tagged entity the effect acts on, for the effects that act on somebody rather than on the
    /// room. Nearest match on the cue's own grid wins.
    /// </summary>
    [DataField]
    public string? TargetTag;

    /// <summary>
    /// Tagged bystander who turns to face <see cref="TargetTag"/> as the cue fires.
    /// </summary>
    /// <remarks>
    /// Rotation is only written while an entity is moving, so whoever walks into a scene keeps
    /// whichever way their last step left them pointing. For a set piece staged around two people
    /// that is a coin flip, and half the time the arrest happens back to back.
    /// </remarks>
    [DataField]
    public string? FaceTag;

    /// <summary>Line said by <see cref="TutorialCueEffect.Speak"/>, in the target's own voice.</summary>
    [DataField]
    public LocId? Line;

    /// <summary>How long <see cref="TutorialCueEffect.Detain"/> holds the target down.</summary>
    [DataField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(30);

    /// <summary>Restraints <see cref="TutorialCueEffect.Detain"/> puts on the target.</summary>
    [DataField]
    public EntProtoId Handcuffs = "Handcuffs";

    /// <summary>
    /// Charge <see cref="TutorialCueEffect.Breach"/> sets off. Defaults are C4's, enough to take
    /// out the window it is placed against and not much else.
    /// </summary>
    [DataField]
    public string ExplosionType = "DemolitionCharge";

    /// <inheritdoc cref="ExplosionType"/>
    [DataField]
    public float TotalIntensity = 60f;

    /// <inheritdoc cref="ExplosionType"/>
    [DataField]
    public float IntensitySlope = 5f;

    /// <inheritdoc cref="ExplosionType"/>
    [DataField]
    public float MaxIntensity = 30f;

    /// <summary>Set once this has gone off, so walking back into the chamber cannot repeat it.</summary>
    [ViewVariables]
    public bool Fired;

    /// <summary>When the armed cue goes off. Null while it is waiting for its sub-goal.</summary>
    [ViewVariables]
    public TimeSpan? FireAt;

    /// <summary>Participant who armed it, so the effect can be aimed at them.</summary>
    [ViewVariables]
    public EntityUid? ArmedBy;

    /// <summary>Set once <see cref="AfterLine"/> has pulled <see cref="FireAt"/> onto that line.</summary>
    [ViewVariables]
    public bool CuedOnLine;
}

[Serializable, NetSerializable]
public enum TutorialCueEffect : byte
{
    /// <summary>Kill every powered light in range, on the same grid.</summary>
    LightsOff,

    /// <summary>Bring them back.</summary>
    LightsOn,

    /// <summary>Blow this entity up. Used on a hull panel with space behind it, so the chamber vents.</summary>
    Breach,

    /// <summary>
    /// Sound and <see cref="TutorialCueComponent.Spawn"/> only. For walking somebody into the
    /// scene on a beat of dialogue without doing anything else to the room.
    /// </summary>
    Stage,

    /// <summary>
    /// A tagged bystander says one line, in their own voice rather than the coach's. For the
    /// scripted exchanges a curriculum stages around the player instead of at them.
    /// </summary>
    Speak,

    /// <summary>
    /// Throws a tagged signal switch, as if somebody in the room had pressed it. Whatever the
    /// mapper linked it to happens: shutters, doors, lights. The point is that the station reacts
    /// to the player rather than being a diorama.
    /// </summary>
    Press,

    /// <summary>
    /// Put a tagged entity on the floor in restraints. Scripted rather than left to combat AI: a
    /// set piece that has to land on a first-time player's screen every single time is the wrong
    /// place for emergent behaviour.
    /// </summary>
    Detain,

    /// <summary>
    /// Light the holopad this cue sits on and project <see cref="TutorialCueComponent.Spawn"/> onto
    /// it. Lets a curriculum whose coach walks still hand a beat to the holographic one, without
    /// the pad standing lit and empty for the hour before she has anything to say.
    /// </summary>
    Project,
}
