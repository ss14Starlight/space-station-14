using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Wires;

[Serializable, NetSerializable]
public sealed class StationAiWireCutHoldMessage : BoundUserInterfaceMessage
{
    public readonly int Id;
    public readonly bool Started;

    public StationAiWireCutHoldMessage(int id, bool started)
    {
        Id = id;
        Started = started;
    }
}
