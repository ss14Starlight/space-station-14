using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent]
public sealed partial class BroadcastRecorderComponent : Component
{
    /// <summary>
    /// Container holding the tape.
    /// </summary>
    [DataField]
    public string Slot = "broadcast_tape";

    /// <summary>
    /// Radio channel whose speech is recorded and played back.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "RadioShow";

    /// <summary>
    /// Invisible entity spawned for every channel of a tape being played, each broadcasting its own track.
    /// </summary>
    [DataField]
    public EntProtoId ChannelPrototype = "BroadcastRecorderChannel";

    [ViewVariables]
    public BroadcastRecorderMode Mode;

    /// <summary>
    /// Tape position while stopped.
    /// </summary>
    [ViewVariables]
    public TimeSpan Position;

    /// <summary>
    /// When the tape would have been at its start, while recording or playing.
    /// </summary>
    [ViewVariables]
    public TimeSpan Start;

    /// <summary>
    /// Tape channel given to each broadcast source.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, int> RecordChannels = new();

    /// <summary>
    /// The track and volume last written for each source, to notice changes.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, (StationRadioTrack Track, float Gain)> RecordState = new();

    /// <summary>
    /// Index of the next tape event to apply.
    /// </summary>
    [ViewVariables]
    public int NextEvent;

    /// <summary>
    /// Entity broadcasting each tape channel.
    /// </summary>
    [ViewVariables]
    public Dictionary<int, EntityUid> PlaybackChannels = new();
}

[Serializable, NetSerializable]
public enum BroadcastRecorderMode : byte
{
    Stopped,
    Recording,
    Playing,
}
