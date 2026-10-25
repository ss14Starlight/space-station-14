using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.BodyControl;

[Serializable, NetSerializable]
public enum BodyControlCommand : byte
{
    SelectHand, CycleHand, CycleHandReverse, UseHand, AltUseHand, ActivateHand, HandClick,
    Drop, Interact, AltInteract, ActivateWorld, Action, ToggleCombat,
    Throw, Pull, Shoot, MovePulledObject, InventoryUse, InventoryStorage, InventoryActivate, InventoryAltActivate, Alert,
}

/// <summary>The server resolves the source and body from the sending session's current grant.</summary>
[Serializable, NetSerializable]
public sealed class BodyControlCommandEvent(BodyControlCommand command, string? hand = null,
    NetEntity? target = null, NetCoordinates? coordinates = null, NetEntity? action = null, string? slot = null) : EntityEventArgs
{
    public readonly BodyControlCommand Command = command;
    public readonly string? Hand = hand;
    public readonly NetEntity? Target = target;
    public readonly NetCoordinates? Coordinates = coordinates;
    public readonly NetEntity? Action = action;
    public readonly string? Slot = slot;
}

[Serializable, NetSerializable]
public sealed record BodyControlAccessState(BodyControlAccess Access, List<string> Hands);

/// <summary>Lets a body controller consume native hand-slot requests without changing the body's active hand.</summary>
[ByRefEvent]
public record struct BodyControlHandRequestEvent(BodyControlCommand Command, string? Hand = null);

[ByRefEvent]
public readonly record struct BodyControlDeniedEvent(string Reason);
