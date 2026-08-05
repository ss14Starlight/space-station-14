using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Computers.RemoteEye.UI;

[Serializable, NetSerializable]
public sealed class RemoteEyeConsoleBuiState : BoundUserInterfaceState
{
    public required Dictionary<int, StationBeacons> Stations { get; init; }
    public required Color Color { get; init; }
}
