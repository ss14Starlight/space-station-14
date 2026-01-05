using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Components;

[Serializable, NetSerializable]
public enum SharedGasTankUiKey : byte
{
    // Starlight edit start - Add an alternative UI for breathable organs
    Key,
    OrganKey
    // Starlight edit end
}

[Serializable, NetSerializable]
public sealed class GasTankToggleInternalsMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class GasTankSetPressureMessage : BoundUserInterfaceMessage
{
    public float Pressure;
}

// Starlight edit start - Add an alternative UI for breathable organs
[Serializable, NetSerializable]
public sealed class GasTankEmptyOrganMessage : BoundUserInterfaceMessage;
// Starlight edit end

[Serializable, NetSerializable]
public sealed class GasTankBoundUserInterfaceState : BoundUserInterfaceState
{
    public float TankPressure;
}
