using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StationRadioReceiverComponent : Component
{
    /// <summary>
    /// Playing sound entities per broadcast source, with the track each one was started for. Client only.
    /// </summary>
    [ViewVariables]
    public Dictionary<NetEntity, (EntityUid Stream, StationRadioTrack Track)> Streams = new();

    /// <summary>
    /// Client side volume.
    /// </summary>
    [ViewVariables]
    public float? ClientVolume;

    /// <summary>
    /// The songs or advertisements being played, keyed by the source that broadcasts them.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<NetEntity, StationRadioTrack> Tracks = new();

    /// <summary>
    /// How badly the broadcast is disturbed, from 0 to 1: the radio hisses and the music wavers.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float Interference;

    /// <summary>
    /// The hiss being played while <see cref="Interference"/> is above zero. Client only.
    /// </summary>
    [ViewVariables]
    public EntityUid? StaticStream;

    /// <summary>
    /// Looping hiss played under interference.
    /// </summary>
    [DataField]
    public SoundSpecifier StaticSound = new SoundPathSpecifier("/Audio/_Starlight/RadioStation/static.ogg");

    /// <summary>
    /// Volume knob of this radio, from 0 to 1, on top of everything the broadcast sets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Volume = 1f;

    /// <summary>
    /// Clicking the radio switches it on and off. Off for radios with a control panel or built into other machines.
    /// </summary>
    [DataField]
    public bool ToggleOnActivate = true;

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
}
