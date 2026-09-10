using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StationRadioReceiverComponent : Component
{
    /// Starlight edit start
    /// <summary>
    /// The sound entity. Client only.
    /// </summary>
    [ViewVariables]
    public EntityUid? SoundEntity;

    /// <summary>
    /// Client side volume.
    /// </summary>
    [ViewVariables]
    public float? ClientVolume;

    /// <summary>
    /// The song or advertisement being played.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? CurrentSound;

    /// <summary>
    /// When CurrentSound started playing
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? StartTime;

    /// <summary>
    /// Is the radio turned on
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    /// Default audio params for the played audio.
    /// </summary>
    /// <remarks>
    /// Do not set volume or gain. it will be reset.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public AudioParams DefaultParams = AudioParams.Default.WithMaxDistance(8f); // 8 is just the edge of the screen usually

    /// <summary>
    /// Increase "volume" by changing range
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BoostVolume;

    /// <summary>
    /// Client state.
    /// </summary>
    public bool BoostVolumePrev;

    /// <summary>
    /// Boosted AudioParams
    /// </summary>
    /// /// <remarks>
    /// Do not set volume or gain. it will be reset.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public AudioParams BoostedParams = AudioParams.Default.WithMaxDistance(12f);
    /// Starlight Edit end
}
