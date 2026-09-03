using Content.Shared.FixedPoint;
using Content.Shared.Sound;
using Content.Shared.Sound.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Sound;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DamageThresholdSoundsComponent : Component
{
    /// Damage thresholds at which point the associated sound specifier will play.
    [DataField(required: true), AutoNetworkedField] public Dictionary<FixedPoint2, ThresholdSoundData?> Thresholds = [];

    /// Reference to the currently playing audio.
    [ViewVariables, AutoNetworkedField] public EntityUid PlayingAudio;

    /// Keeps track of the last threshold value reached to prevent cutting off audio unnecessarily.
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField] public FixedPoint2 CurrentThreshold;
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
}
