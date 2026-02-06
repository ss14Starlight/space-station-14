// IPC Battery Component
// Created by Killer Tamashi and Princess Gurchi for the FH project.
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135

using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Ninja.Components;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.IPC.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class IPCBatteryComponent : Component
{
    [DataField]
    public string BatteryContainerSlotID = "cell_slot";
    
    [DataField]
    public float DieWithoutPowerAfter = 30f;
    
    [DataField]
    public int NumWarnings = 0;
    
    [DataField]
    public LocId? WarningText = null;
    
    [DataField]
    public SoundSpecifier? WarningSound = null;
    
    [DataField]
    public ProtoId<AlertPrototype> ChargeCritical = "IPCBatteryCrit";
    
    [DataField]
    public ProtoId<AlertPrototype> NoBatteryAlert = "IPCBatteryNone";
    
    [DataField]
    public ProtoId<AlertPrototype> BatteryAlert = "IPCBattery";
    
    [DataField]
    public ProtoId<AlertCategoryPrototype> BatteryAlertsCategory = "IPCBattery";

    [DataField]
    public List<EntProtoId> DrainAllowedTargets = [];
    
    [DataField]
    public ProtoId<EmotePrototype>? NoPowerDeathEmote = null;
    
    [ViewVariables(VVAccess.ReadWrite)]
    public ContainerSlot BatteryContainerSlot = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public BatteryDrainerComponent BatteryDrainer = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public PowerCellSlotComponent PowerCellSlot = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool TimerActive = false;
    
    [ViewVariables(VVAccess.ReadWrite)]
    public float Timer = 0f;
    
    public TimeSpan NextUpdate;
    
    [DataField]
    public TimeSpan RefreshRate = TimeSpan.FromSeconds(1);
    
    [ViewVariables(VVAccess.ReadWrite)]
    public int WarningsIssued = 0;
    
    [DataField]
    public TimeSpan AlarmCooldown = TimeSpan.FromSeconds(10);
    
    public TimeSpan NextAlarmTime;
}

[Serializable, NetSerializable]
public sealed class IPCBatteryDeathTimerStart : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class IPCBatteryDeathTimerEnd(bool interrupted = false) : EntityEventArgs
{
    public bool Interrupted = interrupted;
}

[Serializable, NetSerializable]
public sealed class IPCBatteryDeathTimerUpdate : EntityEventArgs;

