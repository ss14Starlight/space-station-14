using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleModsComponent : Component
{
    [DataField]
    public string ModContainer = "vehicle_mods_container";

    [DataField]
    public List<EntProtoId> StartingEquipment = new();

    [DataField]
    public EquipmentType EquipmentSlots;

    [AutoNetworkedField]
    public Dictionary<EquipmentType, EntityUid?> Equipment = new();

    [DataField(required: true)]
    public VehicleType VehicleType;

    [AutoNetworkedField]
    public FixedPoint2 MaxIntegrity;

    public ProtoId<DamageModifierSetPrototype>? OriginalArmor;
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