using Content.Shared._Starlight.StationRadio.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.StationRadio;

[Serializable, NetSerializable]
public enum BroadcastRecorderUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum BroadcastRecorderVisuals : byte
{
    Mode,

    Tape,
}

[Serializable, NetSerializable]
public enum BroadcastRecorderVisualLayers : byte
{
    Tape,
    Reels,
    RecordLight,
}

[Serializable, NetSerializable]
public enum BroadcastRecorderAction : byte
{
    Record,
    Play,
    Stop,
    Rewind,
}

[Serializable, NetSerializable]
public sealed class BroadcastRecorderState(
    BroadcastRecorderMode mode,
    string? tape,
    TimeSpan position,
    TimeSpan start,
    TimeSpan length,
    TimeSpan maxLength,
    bool connected) : BoundUserInterfaceState
{
    public readonly BroadcastRecorderMode Mode = mode;
    public readonly string? Tape = tape;
    public readonly TimeSpan Position = position;
    public readonly TimeSpan Start = start;
    public readonly TimeSpan Length = length;
    public readonly TimeSpan MaxLength = maxLength;
    public readonly bool Connected = connected;
}

[Serializable, NetSerializable]
public sealed class BroadcastRecorderActionMessage(BroadcastRecorderAction action) : BoundUserInterfaceMessage
{
    public readonly BroadcastRecorderAction Action = action;
}
