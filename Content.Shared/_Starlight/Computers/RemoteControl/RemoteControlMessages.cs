using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Content.Shared.Storage;
using Content.Shared.Atmos.Components;
using Robust.Shared.Maths;

namespace Content.Shared._Starlight.Computers.RemoteControl;

[Serializable, NetSerializable]
public enum RemoteControlStorageAction : byte
{
    InteractWithItem,
    SetItemLocation,
    TransferItem,
    InsertItem,
    SaveItemLocation,
}

[Serializable, NetSerializable]
public sealed class RemoteControlStorageMessage : BoundUserInterfaceMessage
{
    public required RemoteControlStorageAction Action { get; init; }
    public required NetEntity Item { get; init; }
    public required NetEntity Storage { get; init; }
    public NetEntity? TargetStorage { get; init; }
    public ItemStorageLocation Location { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum RemoteControlInteractionAction : byte
{
    Interact,
    TryPull,
    MovePulledObject,
}

[Serializable, NetSerializable]
public sealed class RemoteControlInteractionMessage : BoundUserInterfaceMessage
{
    public required NetCoordinates Coordinates { get; init; }
    public NetEntity? Target { get; init; }
    public bool AltInteract { get; init; }
    public bool ActivateInWorld { get; init; }
    public RemoteControlInteractionAction Action { get; init; }
    public AtmosPipeLayer? PipeLayer { get; init; }
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
}

[Serializable, NetSerializable]
public sealed class RemoteControlInventoryMessage : BoundUserInterfaceMessage
{
    public required string Slot { get; init; }
    public required RemoteControlInventoryAction Action { get; init; }
}
