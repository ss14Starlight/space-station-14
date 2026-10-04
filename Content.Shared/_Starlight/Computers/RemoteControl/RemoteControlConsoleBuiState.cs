using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared.Hands.Components;

namespace Content.Shared._Starlight.Computers.RemoteControl;

[Serializable, NetSerializable]
public sealed class RemoteControlConsoleBuiState : BoundUserInterfaceState
{
    public required bool Connected { get; init; }
    public bool EnableRemoteView { get; init; } = true;
    public NetEntity? RemoteEntity { get; init; }
    public NetEntity? Controller { get; init; }
    public NetEntity[] Actions { get; init; } = System.Array.Empty<NetEntity>();
    public NetEntity? SelectedAction { get; init; }
    public NetEntity? QuickConstructionItem { get; init; }
    public RemoteControlHandState[] Hands { get; init; } = System.Array.Empty<RemoteControlHandState>();
    public RemoteControlInventorySlotState[] Inventory { get; init; } = System.Array.Empty<RemoteControlInventorySlotState>();
}

[Serializable, NetSerializable]
public sealed class RemoteControlHandState
{
    public required string Name { get; init; }
    public required HandLocation Location { get; init; }
    public EntProtoId? EmptyRepresentative { get; init; }
    public NetEntity? HeldItem { get; init; }
    public bool Active { get; init; }
}

[Serializable, NetSerializable]
public sealed class RemoteControlInventorySlotState
{
    public required string Name { get; init; }
    public required string Group { get; init; }
    public required string TextureName { get; init; }
    public required string FullTextureName { get; init; }
    public NetEntity? Item { get; init; }
    public bool HasStorage { get; init; }
}
