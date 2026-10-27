using Content.Shared.Input;
using Robust.Shared.Input;
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

/// <summary>The input commands shared by native-HUD control on the client and server.</summary>
public static class BodyControlBindings
{
    public static readonly (BoundKeyFunction Key, BodyControlCommand Command)[] All =
    [
        (EngineKeyFunctions.Use, BodyControlCommand.Interact),
        (EngineKeyFunctions.UseSecondary, BodyControlCommand.Interact),
        (ContentKeyFunctions.AltActivateItemInWorld, BodyControlCommand.AltInteract),
        (ContentKeyFunctions.ActivateItemInWorld, BodyControlCommand.ActivateWorld),
        (ContentKeyFunctions.UseItemInHand, BodyControlCommand.UseHand),
        (ContentKeyFunctions.AltUseItemInHand, BodyControlCommand.AltUseHand),
        (ContentKeyFunctions.SwapHands, BodyControlCommand.CycleHand),
        (ContentKeyFunctions.SwapHandsReverse, BodyControlCommand.CycleHandReverse),
        (ContentKeyFunctions.Drop, BodyControlCommand.Drop),
        (ContentKeyFunctions.ThrowItemInHand, BodyControlCommand.Throw),
        (ContentKeyFunctions.TryPullObject, BodyControlCommand.Pull),
    ];
}

/// <summary>Lets control providers close their interfaces before a cooperative grant is removed.</summary>
[ByRefEvent]
public readonly record struct BodyControlStoppingEvent(EntityUid Controller, EntityUid Body);
