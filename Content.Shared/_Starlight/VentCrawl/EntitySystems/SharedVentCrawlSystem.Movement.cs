using System.Numerics;
using Content.Shared._Starlight.VentCrawl.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;

namespace Content.Shared._Starlight.VentCrawl.EntitySystems;

public sealed partial class SharedVentCrawlSystem
{
    public void InitializeMovement()
    {
        SubscribeLocalEvent<VentCrawlHolderComponent, MoveInputEvent>(OnMoveInput);
    }

    /// <summary>
    /// Handles the MoveInputEvent for <seealso cref="VentCrawlHolderComponent"/>.
    /// </summary>
    private void OnMoveInput(EntityUid uid, VentCrawlHolderComponent holder, ref MoveInputEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        if (!Exists(holder.CurrentTube))
        {
            ExitVentCrawl(uid);
            return;
        }

        var dir = args.Dir;
        if (dir != Direction.Invalid && TryComp<InputMoverComponent>(holder.ContainedEntity, out var mover))
        {
            var cameraAngle = mover.TargetRelativeRotation;
            if (cameraAngle != Angle.Zero)
                dir = (dir.ToAngle() + cameraAngle).GetCardinalDir();
        }

        holder.IsMoving = args.HasDirectionalMovement;
        DirtyField(uid, holder, nameof(VentCrawlHolderComponent.IsMoving));

        if (holder.CurrentDirection != dir && dir != Direction.Invalid)
        {
            if (holder.NextTube != null)
            {
                var total = (holder.MoveEndTime - holder.MoveStartTime).TotalSeconds;
                var elapsed = total > 0
                    ? (_gameTiming.CurTime - holder.MoveStartTime).TotalSeconds
                    : 1.0;

                if (elapsed / total < 0.5)
                {
                    holder.NextTube = null;
                    holder.MoveStartTime = TimeSpan.Zero;
                    holder.MoveEndTime = TimeSpan.Zero;
                    DirtyField(uid, holder, nameof(VentCrawlHolderComponent.NextTube));
                }
            }

            holder.PreviousDirection = holder.CurrentDirection;
            holder.CurrentDirection = dir;
            DirtyField(uid, holder, nameof(VentCrawlHolderComponent.CurrentDirection));
            DirtyField(uid, holder, nameof(VentCrawlHolderComponent.PreviousDirection));
        }
    }

    private bool UpdateMovementInput(EntityUid currentTube, EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (!holder.IsMoving)
        {
            holder.CurrentDirection = Direction.Invalid;

            var snapTube = currentTube;

            if (holder.NextTube != null)
            {
                var total = (holder.MoveEndTime - holder.MoveStartTime).TotalSeconds;
                var elapsed = total > 0
                    ? (_gameTiming.CurTime - holder.MoveStartTime).TotalSeconds
                    : 1.0;

                var progress = elapsed / total;

                if (progress >= 0.5f)
                    snapTube = holder.NextTube.Value;
            }

            holder.NextTube = null;
            holder.MoveStartTime = TimeSpan.Zero;
            holder.MoveEndTime = TimeSpan.Zero;

            if (TryComp<VentCrawlManifoldComponent>(snapTube, out _))
            {
                holder.CurrentTube = snapTube;
                UpdateManifoldPosition(snapTube, uid, holder);
            }
            else
            {
                holder.CurrentTube = snapTube;
                _xformSystem.SetWorldPosition(uid, ComputeTubeWorldPos(snapTube));
            }

            Dirty(uid, holder);
            UpdateVisionBlocking(uid, holder.ContainedEntity, holder.CurrentTube.Value);
            UpdateExitAction(holder, holder.CurrentTube.Value);
            return true;
        }

        if (holder.CurrentDirection == Direction.Invalid)
            return true;

        if (TryComp<VentCrawlManifoldComponent>(currentTube, out var manifold))
            return UpdateManifoldInput(currentTube, uid, holder, manifold);

        if (holder.NextTube == null || holder.NextTube == holder.CurrentTube)
        {
            var nextTube = NextTubeFor(currentTube, holder.CurrentDirection);

            if (nextTube != null && TryComp<VentCrawlerComponent>(holder.ContainedEntity, out var crawler))
            {
                if (!Exists(holder.CurrentTube))
                {
                    ExitVentCrawl(uid);
                    return false;
                }

                if (nextTube == holder.CurrentTube)
                    return false;

                BeginMoveTo(uid, nextTube.Value, holder, crawler.Speed);
            }
            else
            {
                var ev = new GetVentCrawlsConnectableDirectionsEvent();
                RaiseLocalEvent(currentTube, ref ev);
                if (ev.Connectable.Contains(holder.CurrentDirection))
                {
                    ExitVentCrawl(uid);
                    return false;
                }
            }
        }

        return true;
    }

    private bool UpdateManifoldInput(
        EntityUid manifoldUid,
        EntityUid uid,
        VentCrawlHolderComponent holder,
        VentCrawlManifoldComponent manifold)
    {
        if (!holder.IsMoving)
            return true;

        if (holder.CurrentDirection == Direction.Invalid)
            return true;

        if (!TryComp<VentCrawlerComponent>(holder.ContainedEntity, out var crawler))
        {
            ExitVentCrawl(uid);
            return false;
        }

        if (holder.ManifoldLayer == null && holder.PreviousTube != null && TryComp<AtmosPipeLayersComponent>(holder.PreviousTube, out var previousLayer))
            holder.ManifoldLayer = previousLayer.CurrentPipeLayer;

        holder.ManifoldLayer ??= AtmosPipeLayer.Primary;
        DirtyField(uid, holder, nameof(VentCrawlHolderComponent.ManifoldLayer));

        var rotation = Transform(manifoldUid).WorldRotation;

        var right = rotation.RotateVec(Vector2.UnitX);
        var forward = rotation.RotateVec(Vector2.UnitY);

        Vector2 inputVec = holder.CurrentDirection.ToVec();

        float rightDot = Vector2.Dot(inputVec, right);
        float forwardDot = Vector2.Dot(inputVec, forward);

        var absRight = Math.Abs(rightDot);
        var absForward = Math.Abs(forwardDot);

        if (absRight > absForward)
        {
            if (_gameTiming.CurTime < holder.ManifoldLastLayerSelection + holder.ManifoldLayerSelectionCooldown)
                return true;

            holder.ManifoldLastLayerSelection = _gameTiming.CurTime;

            var offset = LayerToOffset(holder.ManifoldLayer.Value);

            offset += Math.Sign(rightDot);

            offset = Math.Clamp(offset, -2, 2);

            var newLayer = OffsetToLayer(offset);

            if (newLayer != holder.ManifoldLayer)
            {
                holder.PreviousManifoldLayer = holder.ManifoldLayer;
                holder.ManifoldLayer = newLayer;
                holder.ManifoldTransitionStart = _gameTiming.CurTime;
                holder.ManifoldTransitionEnd = _gameTiming.CurTime + TimeSpan.FromSeconds(holder.ManifoldTransitionDuration);
                holder.NextTube = null;
                holder.MoveStartTime = TimeSpan.Zero;
                holder.MoveEndTime = TimeSpan.Zero;
                Dirty(uid, holder);
            }
            return true;
        }

        if (absRight < absForward)
        {
            if (_gameTiming.CurTime < holder.ManifoldTransitionEnd)
                return true;

            if (holder.NextTube != null)
                return true;

            var nextTube = GetManifoldExit(manifoldUid, holder.ManifoldLayer.Value, holder.CurrentDirection);
            if (nextTube == null || holder.CurrentTube == nextTube)
                return true;

            BeginMoveTo(uid, nextTube.Value, holder, crawler.Speed);
        }

        return true;
    }
}
