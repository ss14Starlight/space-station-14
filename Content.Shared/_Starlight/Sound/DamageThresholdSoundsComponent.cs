using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Sound;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DamageThresholdSoundsComponent : Component
{
    /// Damage thresholds at which point the associated sound specifier will play.
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<FixedPoint2, ThresholdSoundData?> Thresholds = [];

    /// Reference to the currently playing audio.
    [ViewVariables]
    public Entity<AudioComponent?>? AudioStream;

    /// Keeps track of the last threshold value reached to prevent cutting off audio unnecessarily.
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 CurrentThreshold;

    /// <summary>
    /// Keeps track of reither the Entity is currrently disabled via Emp
    /// </summary>
    /// <remarks>
    /// use <see cref="ThresholdSoundData"/> for setting if Emps should cancel sounds
    /// </remarks>
    [AutoNetworkedField]
    public bool IsEmped = false;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ThresholdSoundData
{
    /// The sound to play.
    [DataField] public SoundSpecifier? Sound;

    /// Determines if it should emit the sound once or loop the sound as ambience.
    /// <remarks>
    /// Yes you can just mess with audio parameters, but like this is easier.
    /// </remarks>
    [DataField] public bool Ambient;

    /// <summary>
    /// Should an Emp pulse prevent this audio from playing?
    /// </summary>
    [DataField] public bool EmpEffected;
}
