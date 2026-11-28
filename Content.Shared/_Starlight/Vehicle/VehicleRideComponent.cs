using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleRideComponent : Component
{
    [DataField]
    public SoundSpecifier? HornSound;

    [DataField]
    public EntProtoId HornAction = "ActionVehicleHorn";

    [AutoNetworkedField]
    public EntityUid? HornActionEntity;

    [DataField]
    public EntProtoId? TrunkAction;

    [AutoNetworkedField]
    public EntityUid? TrunkActionEntity;

    [DataField]
    public bool DismountOnStun;

    [DataField]
    public bool DismountOnKnockdown;

    [DataField]
    public TimeSpan? UnbuckleDelay;

    [DataField]
    public bool RiderSpeedAffectsVehicle;

    [DataField]
    public bool DisallowWielding;

    [DataField]
    public bool GunStaminaRecoil;

    [DataField]
    public float GunSpreadPenalty;

    [DataField]
    public bool EjectOnCrash;

    [DataField]
    public DamageSpecifier? DriverCrashDamage;

    [DataField]
    public SoundSpecifier? ImpactSound;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleHullComponent : Component
{
    [AutoNetworkedField]
    public bool Broken;
}

[Serializable, NetSerializable]
public enum VehicleHullVisuals : byte
{
    Broken,
}

[Serializable, NetSerializable]
public sealed partial class VehicleUnbuckleDoAfter : SimpleDoAfterEvent
{
    public readonly NetEntity Driver;

    public VehicleUnbuckleDoAfter(NetEntity driver)
        => Driver = driver;
}

public sealed partial class HornActionEvent : InstantActionEvent;
public sealed partial class ToggleTrunkActionEvent : InstantActionEvent;
