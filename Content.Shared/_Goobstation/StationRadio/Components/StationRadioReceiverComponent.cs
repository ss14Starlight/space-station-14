using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationRadioReceiverComponent : Component
{
    /// <summary>
    /// The sound entity being played
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SoundEntity;

    /// <summary>
    /// Is the radio turned on
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    /// Default audio params for the played audio.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AudioParams DefaultParams = AudioParams.Default.WithVolume(3.5f).WithMaxDistance(8f); // 8 is just the edge of the screen usually

    /// <summary>
    /// Is Radio is playing at full or low volume.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LowVolume;

    /// <summary>
    /// How quiet the low volume setting on a Station Radio is. Default is 10%.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LowVolumeGain = 0.1f;
}
