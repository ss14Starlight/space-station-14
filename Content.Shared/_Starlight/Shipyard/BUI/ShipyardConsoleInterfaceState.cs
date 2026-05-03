using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shipyard.BUI;

[NetSerializable, Serializable]
public sealed class ShipyardConsoleInterfaceState : BoundUserInterfaceState
{
    public readonly int Balance;
    public readonly bool AccessGranted;

    public ShipyardConsoleInterfaceState(
        int balance,
        bool accessGranted)
    {
        Balance = balance;
        AccessGranted = accessGranted;
    }
}
