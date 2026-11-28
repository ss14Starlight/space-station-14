using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Vehicle;

/// <summary>
/// Adds capacity and passenger interactions to the vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VehiclePassengerSeatsComponent : Component
{
    [DataField]
    public int Seats = 2;

    [DataField]
    public EntityWhitelist? PassengerWhitelist;

    [DataField]
    public float DamageTransfer = 0.5f;

    [DataField]
    public TimeSpan RemoveTime = TimeSpan.FromSeconds(1.5);
}

[Serializable, NetSerializable]
public sealed partial class RemoveVehiclePassengerDoAfter : SimpleDoAfterEvent
{
    public readonly NetEntity Passenger;

    public RemoveVehiclePassengerDoAfter(NetEntity passenger)
        => Passenger = passenger;
}
