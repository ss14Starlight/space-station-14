namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent]
public sealed partial class StationRadioServerComponent : Component
{
    /// <summary>
    /// The tracks currently being broadcasted, keyed by the source playing them.
    /// Empty if nothing is playing.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, StationRadioTrack> Tracks = new();

    /// <summary>
    /// Mixer volume of each source, from 0 to 1. Sources without an entry play at full volume.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, float> SourceGains = new();

    /// <summary>
    /// Mixer volume applied on top of every source.
    /// </summary>
    [DataField]
    public float MasterGain = 1f;

    /// <summary>
    /// Mixer volume of the host's voice.
    /// </summary>
    [DataField]
    public float VoiceGain = 1f;

    #region Wires

    /// <summary>
    /// Music wire cutted?
    /// </summary>
    [ViewVariables]
    public bool MusicCut;

    /// <summary>
    /// Music disabled until.
    /// </summary>
    [ViewVariables]
    public TimeSpan MusicMutedUntil;

    /// <summary>
    /// Microphone wire cutted?
    /// </summary>
    [ViewVariables]
    public bool MicrophoneCut;

    /// <summary>
    /// Microphone disabled until.
    /// </summary>
    [ViewVariables]
    public TimeSpan MicrophoneMutedUntil;

    /// <summary>
    /// Interference wire cutted?
    /// </summary>
    [ViewVariables]
    public bool InterferenceCut;

    /// <summary>
    /// The broadcast hisses until.
    /// </summary>
    [ViewVariables]
    public TimeSpan InterferenceUntil;

    #endregion

    #region Interference

    /// <summary>
    /// How badly the broadcast is disturbed, from 0 to 1, as last sent to the radios.
    /// </summary>
    [ViewVariables]
    public float Interference;

    /// <summary>
    /// Whether music is kept off the air right now, as last sent to the radios.
    /// </summary>
    [ViewVariables]
    public bool MusicMuted;

    /// <summary>
    /// Damage at which the broadcast starts to crackle.
    /// </summary>
    [DataField]
    public float DamageInterferenceStart = 30f;

    /// <summary>
    /// Damage at which the broadcast is as disturbed as damage alone can make it.
    /// </summary>
    [DataField]
    public float DamageInterferenceFull = 150f;

    /// <summary>
    /// Interference while the interference wire is cut.
    /// </summary>
    [DataField]
    public float WireInterference = 1f;

    /// <summary>
    /// Interference after an EMP or a pulsed interference wire.
    /// </summary>
    [DataField]
    public float BurstInterference = 0.8f;

    /// <summary>
    /// Interference from damage alone at <see cref="DamageInterferenceFull"/>.
    /// </summary>
    [DataField]
    public float DamageInterference = 0.7f;

    #endregion

    #region Listeners

    /// <summary>
    /// Players near a working station radio of this server at the last count.
    /// </summary>
    [ViewVariables]
    public int Listeners;

    [ViewVariables]
    public TimeSpan NextListenerCount;

    [DataField]
    public TimeSpan ListenerCountInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// When each advertisement last paid out; the same ad pays nothing again until <see cref="AdCooldown"/> passes.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, TimeSpan> AdsPaid = new();

    [DataField]
    public TimeSpan AdCooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// After any advert pays, no advert pays again until this passes, so adverts can't be chained for money.
    /// </summary>
    [DataField]
    public TimeSpan AdGlobalCooldown = TimeSpan.FromMinutes(5);

    [ViewVariables]
    public TimeSpan LastAdvertPaid = TimeSpan.MinValue;

    /// <summary>
    /// How well each advert disc playing now has come across so far, from 0 to 1 per sample.
    /// An advert under music or talk, or played quietly, earns less.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, (float Score, int Samples)> AdvertAirtime = new();

    [ViewVariables]
    public TimeSpan NextAdvertSample;

    [DataField]
    public TimeSpan AdvertSampleInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The host is heard talking on air until then.
    /// </summary>
    [ViewVariables]
    public TimeSpan VoiceOnAirUntil;

    #endregion
}
