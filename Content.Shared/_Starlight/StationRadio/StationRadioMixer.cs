using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio;

[Serializable, NetSerializable]
public enum StationRadioMixerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum StationRadioMixerFader : byte
{
    Source,

    Master,

    Voice,

    Monitor,
}

/// <summary>
/// A source channel strip on the mixer.
/// </summary>
[Serializable, NetSerializable]
public record struct StationRadioMixerChannel(
    NetEntity Source,
    string Name,
    float Gain,
    bool Playing,
    string? Disc,
    List<string?> Slots,
    int CurrentSlot,
    TimeSpan Length,
    TimeSpan Position,
    TimeSpan PlaybackStart);

[Serializable, NetSerializable]
public enum StationRadioMixerAction : byte
{
    TogglePlay,

    Rewind,

    Next,

    Select,

    Eject,
}

[Serializable, NetSerializable]
public sealed class StationRadioMixerState(
    List<StationRadioMixerChannel> channels,
    float masterGain,
    float voiceGain,
    bool connected,
    int listeners = 0,
    float interference = 0f,
    float? monitorGain = null) : BoundUserInterfaceState
{
    /// <summary>
    /// Volume of the mixer's own radio, null if it has none.
    /// </summary>
    public readonly float? MonitorGain = monitorGain;

    /// <summary>
    /// Players near a working station radio at the server's last count.
    /// </summary>
    public readonly int Listeners = listeners;

    /// <summary>
    /// How badly the broadcast is disturbed, from 0 to 1.
    /// </summary>
    public readonly float Interference = interference;

    public readonly List<StationRadioMixerChannel> Channels = channels;
    public readonly float MasterGain = masterGain;
    public readonly float VoiceGain = voiceGain;

    /// <summary>
    /// Whether the mixer found a working radio server on its grid.
    /// </summary>
    public readonly bool Connected = connected;
}

[Serializable, NetSerializable]
public sealed class StationRadioMixerSetGainMessage(StationRadioMixerFader fader, NetEntity? source, float gain)
    : BoundUserInterfaceMessage
{
    public readonly StationRadioMixerFader Fader = fader;
    public readonly NetEntity? Source = source;
    public readonly float Gain = gain;
}

[Serializable, NetSerializable]
public sealed class StationRadioMixerTransportMessage(NetEntity source, StationRadioMixerAction action, int slot = 0)
    : BoundUserInterfaceMessage
{
    public readonly NetEntity Source = source;
    public readonly StationRadioMixerAction Action = action;
    public readonly int Slot = slot;
}

[Serializable, NetSerializable]
public enum StationRadioServerWireStatus : byte
{
    Music,
    Microphone,
    Interference,
}

[Serializable, NetSerializable]
public enum StationRadioReceiverUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StationRadioReceiverVolumeMessage(float volume) : BoundUserInterfaceMessage
{
    public readonly float Volume = volume;
}

[Serializable, NetSerializable]
public sealed class StationRadioReceiverPowerMessage : BoundUserInterfaceMessage;
