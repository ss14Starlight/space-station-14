using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shuttles;

[Serializable, NetSerializable]
public enum AdjustableThrusterUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AdjustableThrusterBuiState : BoundUserInterfaceState
{
    public float Thrust;
    public float MinThrust;
    public float MaxThrust;

    public AdjustableThrusterBuiState(float thrust, float minThrust, float maxThrust)
    {
        Thrust = thrust;
        MinThrust = minThrust;
        MaxThrust = maxThrust;
    }
}

[Serializable, NetSerializable]
public sealed class AdjustableThrusterSetThrustMessage : BoundUserInterfaceMessage
{
    public float Thrust;

    public AdjustableThrusterSetThrustMessage(float thrust) => Thrust = thrust;
}
