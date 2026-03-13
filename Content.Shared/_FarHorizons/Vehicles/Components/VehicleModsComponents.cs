using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleModsComponent : Component
{
    [DataField]
    public string ModContainer = "vehicle_mods_container";

    [ViewVariables(VVAccess.ReadWrite)]
    public Container ModSlot = default!;

    [DataField]
    public HashSet<EntProtoId> StartingEquipment = new();

    [ViewVariables, AutoNetworkedField]
    public HashSet<EntityUid> SpawnedEquipment = new();

    [DataField]
    [Access(typeof(EquipmentType), Other = AccessPermissions.ReadExecute)]
    public EquipmentType EquipmentSlots = EquipmentType.NONE;

    [ViewVariables, AutoNetworkedField]
    public Dictionary<EquipmentType, EntityUid?> Equipment = new();

    [DataField(required:true)]
    public VehicleType VehicleType = VehicleType.None;
}

public sealed class InstalledVehicleEquipment : EntityEventArgs
{
    public EntityUid Vehicle;
}

public sealed class UnInstalledVehicleEquipment : EntityEventArgs
{
    public EntityUid Vehicle;
}

[Serializable, NetSerializable]
[Flags]
public enum VehicleType
{
    None = 0,
    Electric = 1 << 0,
    Gas = 1 << 1,
    Spaceship = 1 << 2
}