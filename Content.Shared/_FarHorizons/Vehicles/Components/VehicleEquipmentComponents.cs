using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VehicleEquipmentComponent : Component
{
    [DataField]
    public EntProtoId? ActionProto;
    
    [ViewVariables, AutoNetworkedField] 
    public EntityUid? ActionEntity;

    [DataField(required: true)]
    public EquipmentType Slot = EquipmentType.NONE;

    [DataField(required: true)]
    public VehicleType AllowedVehicles = VehicleType.None;

    [DataField]
    public TimeSpan InstallandRemoveTime = TimeSpan.FromSeconds(5);

    [DataField]
    public int Health = 100;

    [DataField]
    public float damageTransfer = 0.1f;

    [DataField]
    public float damageChance = 0.33f;
}

public static class EquipmentTypeExtensions
{
    public static IEnumerable<EquipmentType> GetFlags(this EquipmentType value)
    {
        foreach (EquipmentType flag in Enum.GetValues<EquipmentType>())
        {
            if (flag == EquipmentType.NONE)
                continue;

            if (value.HasFlag(flag))
                yield return flag;
        }
    }
}

[Serializable, NetSerializable]
public enum EquipmentVisuals : byte
{
    Hidden
}

[Serializable, NetSerializable]
[Flags]
public enum EquipmentType
{
    NONE = 0,
    TIRES = 1 << 0,
    ENGINE = 1 << 1,
    HEADLIGHT = 1 << 2,
    LIGHTBAR = 1 << 3,
    AIRTANK = 1 << 4,
    VENTFAN = 1 << 5,
    THURSTERS = 1 << 6,
    BOOSTER = 1 << 7,
    ARMOR = 1 << 8
}