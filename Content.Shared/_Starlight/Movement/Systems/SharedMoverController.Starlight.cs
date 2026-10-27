using Content.Shared._Starlight.BodyControl;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Robust.Shared.Maths;

// ReSharper disable CheckNamespace
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    /// <summary>
    /// Applies changes to held movement through the ordinary direction and walk handlers.
    /// This preserves normal movement checks and container handling for server-arbitrated input.
    /// </summary>
    public void ApplyMovementInput(EntityUid entity, MoveButtons buttons)
    {
        if (!MoverQuery.TryComp(entity, out var mover))
            return;
        var previous = mover.HeldMoveButtons;
        ApplyDirection(MoveButtons.Up, Direction.North);
        ApplyDirection(MoveButtons.Down, Direction.South);
        ApplyDirection(MoveButtons.Left, Direction.West);
        ApplyDirection(MoveButtons.Right, Direction.East);
        if ((previous & MoveButtons.Walk) != (buttons & MoveButtons.Walk))
            HandleRunChange(entity, 0, (buttons & MoveButtons.Walk) != 0);

        void ApplyDirection(MoveButtons button, Direction direction)
        {
            if ((previous & button) != (buttons & button))
                HandleDirChange(entity, direction, 0, (buttons & button) != 0);
        }
    }

    /// <summary>
    /// Clears held input and subtick movement accumulated by the previous input source.
    /// Call when changing control routing so the old source cannot leave the body moving.
    /// </summary>
    public void ResetMovementInput(EntityUid entity)
    {
        if (!MoverQuery.TryComp(entity, out var mover))
            return;
        SetMoveInput((entity, mover), MoveButtons.None);
        mover.LastInputTick = Timing.CurTick;
        mover.LastInputSubTick = 0;
        mover.CurTickWalkMovement = default;
        mover.CurTickSprintMovement = default;
        SetWishDir((entity, mover), default);
    }

    private bool RouteMoveInput(EntityUid entity, Direction direction, ushort subTick, bool pressed)
    {
        var button = direction switch
        {
            Direction.North => MoveButtons.Up,
            Direction.South => MoveButtons.Down,
            Direction.West => MoveButtons.Left,
            _ => MoveButtons.Right,
        };
        return RouteMoveInput(entity, button, subTick, pressed);
    }

    private bool RouteMoveInput(EntityUid entity, MoveButtons button, ushort subTick, bool pressed)
    {
        var attempt = new BodyControlMoveInputEvent(button, subTick, pressed);
        RaiseLocalEvent(entity, ref attempt);
        return attempt.Handled;
    }

    private void OnRelayTargetMobStateChanged(
        EntityUid uid,
        MovementRelayTargetComponent component,
        ref MobStateChangedEvent args)
    {
        if (_mobState.IsIncapacitated(uid))
            StopRelayMovement(uid);
    }
}
