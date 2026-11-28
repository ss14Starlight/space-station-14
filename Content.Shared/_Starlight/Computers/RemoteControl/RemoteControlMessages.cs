using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Atmos.Components;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Computers.RemoteControl;

[Serializable, NetSerializable]
public enum RemoteControlUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum RemoteControlSettingsUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RemoteControlOpenSettingsMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RemoteControlOpenScreenMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RemoteControlSetChannelMessage : BoundUserInterfaceMessage
{
    public string? Channel { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RemoteControlToggleKnockdownMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RemoteControlReleasePulledObjectMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum RemoteControlInteractionAction : byte
{
    Interact,
    TryPull,
    MovePulledObject,
    Shoot,
    Drop,
    Throw,
    HeavyAttack,
}

[Serializable, NetSerializable]
public sealed class RemoteControlInteractionMessage : BoundUserInterfaceMessage
{
    public required NetCoordinates Coordinates { get; init; }
    public NetEntity? Target { get; init; }
    public bool AltInteract { get; init; }
    public bool ActivateInWorld { get; init; }
    public RemoteControlInteractionAction Action { get; init; }
    public GameTick Tick { get; init; }
    public AtmosPipeLayer? PipeLayer { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlCloseQuickConstructionMessage : BoundUserInterfaceMessage
{
    public required NetEntity Item { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlDragDropMessage : BoundUserInterfaceMessage
{
    public required NetEntity Dragged { get; init; }
    public required NetEntity Target { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlBuildConstructionMessage : BoundUserInterfaceMessage
{
    public required NetCoordinates Location { get; init; }
    public required string PrototypeName { get; init; }
    public required Angle Angle { get; init; }
    public required int Ack { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlBuildItemConstructionMessage : BoundUserInterfaceMessage
{
    public required string PrototypeName { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlActionMessage : BoundUserInterfaceMessage
{
    public required NetEntity Action { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlTargetActionMessage : BoundUserInterfaceMessage
{
    public required NetEntity Action { get; init; }
    public NetEntity? Target { get; init; }
    public required NetCoordinates Coordinates { get; init; }
}

[Serializable, NetSerializable]
public enum RemoteControlHandAction : byte
{
    SetActive,
    Use,
    Activate,
    AltUse,
    MoveToActive,
    Drop,
    CycleActive,
    InteractWithHand,
    CycleActiveReverse,
}

[Serializable, NetSerializable]
public sealed class RemoteControlHandMessage : BoundUserInterfaceMessage
{
    public required string Hand { get; init; }
    public required RemoteControlHandAction Action { get; init; }
}

[Serializable, NetSerializable]
public enum RemoteControlInventoryAction : byte
{
    UseSlot,
    OpenStorage,
    ActivateSlot,
    AltActivateSlot,
}

[Serializable, NetSerializable]
public sealed class RemoteControlInventoryMessage : BoundUserInterfaceMessage
{
    public required string Slot { get; init; }
    public required RemoteControlInventoryAction Action { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlAlertMessage : BoundUserInterfaceMessage
{
    public required ProtoId<AlertPrototype> Alert { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlBuiOpenedByRemoteActorEvent(NetEntity uiEntity, Enum uiKey, NetEntity remoteActor)
    : EntityEventArgs
{
    public readonly NetEntity UiEntity = uiEntity;
    public readonly string UiKey = GetUiKeyIdentifier(uiKey);
    public readonly NetEntity RemoteActor = remoteActor;

    public static string GetUiKeyIdentifier(Enum uiKey)
        => $"{uiKey.GetType().FullName}:{uiKey}";
}
