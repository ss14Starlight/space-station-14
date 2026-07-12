using System.Linq;
using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Content.Shared.DoAfter;
using Content.Shared._Starlight.VentCrawl.Components;

namespace Content.Shared._Starlight.VentCrawl.EntitySystems;

/// <summary>
/// A system that handles the crawling behavior for vent creatures.
/// </summary>
public sealed partial class SharedVentCrawlSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeingVentCrawlComponent, ExitVentActionEvent>(OnExitVentActionEvent);

        InitializeTubes();
        InitializeClothing();
        InitializeMovement();
    }

    /// <summary>
    /// Attempts to make the <seealso cref="VentCrawlHolderComponent"/> enter a <seealso cref="VentCrawlTubeComponent"/>.
    /// </summary>
    /// <returns>True if the <seealso cref="VentCrawlHolderComponent"/> successfully enters the <seealso cref="VentCrawlTubeComponent"/>; otherwise, False.</returns>
    public bool EnterTube(EntityUid holderUid, EntityUid toUid, VentCrawlHolderComponent? holder = null, TransformComponent? holderTransform = null, VentCrawlTubeComponent? to = null, TransformComponent? toTransform = null)
    {

        if (!Exists(holderUid))
            return false;

        if (!Resolve(holderUid, ref holder, ref holderTransform))
            return false;

        if (holder.IsExitingVentCrawls)
            return false;

        if (!Resolve(toUid, ref to, ref toTransform, false))
            return false;

        var player = holder.ContainedEntity;

        var beingcrawl = EnsureComp<BeingVentCrawlComponent>(player);
        beingcrawl.Holder = holderUid;
        Dirty(player, beingcrawl);

        UpdateExitAction(holder, toUid);

        if (holder.CurrentTube == null)
            _xformSystem.SetWorldPosition(holderUid, ComputeTubeWorldPos(toUid));

        if (HasComp<VentCrawlManifoldComponent>(toUid) && holder.CurrentTube != null && TryComp<AtmosPipeLayersComponent>(holder.CurrentTube, out var curLayers))
        {
            holder.ManifoldLayer = curLayers.CurrentPipeLayer;

            holder.PreviousManifoldLayer = null;
            holder.ManifoldTransitionStart = TimeSpan.Zero;
            holder.ManifoldTransitionEnd = TimeSpan.Zero;

            UpdateManifoldPosition(toUid, holderUid, holder);
        }
        else
            holder.ManifoldLayer = null;

        holder.PreviousTube = holder.CurrentTube;
        if (holder.PreviousTube != null && TryComp<VentCrawlTubeComponent>(holder.PreviousTube, out var prevTube))
        {
            prevTube.ContainedHolders.Remove(holderUid);
            Dirty(holder.PreviousTube.Value, prevTube);
        }
        holder.PreviousDirection = holder.CurrentDirection;
        holder.CurrentTube = toUid;
        Dirty(holderUid, holder);
        UpdateVisionBlocking(holderUid, player, toUid);

        to.ContainedHolders.Add(holderUid);
        Dirty(toUid, to);

        return true;
    }

    private void OnExitVentActionEvent(EntityUid uid, BeingVentCrawlComponent component, ExitVentActionEvent args)
        => ExitVentCrawl(component.Holder);

    /// <summary>
    /// Exits the vent craws for the specified <seealso cref="VentCrawlHolderComponent"/>, removing it and any contained entities from the craws.
    /// </summary>
    public void ExitVentCrawl(EntityUid uid, VentCrawlHolderComponent? holder = null, TransformComponent? holderTransform = null)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        if (Terminating(uid) || Deleted(uid))
            return;

        if (!Resolve(uid, ref holder, ref holderTransform, false))
            return;

        if (holder.IsExitingVentCrawls)
            return;

        holder.IsExitingVentCrawls = true;

        UpdateExitAction(holder);

        var container = GetOrEnsureContainer(uid);

        foreach (var entity in container.ContainedEntities.ToArray())
        {
            RemComp<BeingVentCrawlComponent>(entity);

            _containerSystem.Remove(entity, container, reparent: false, force: true);

            var xform = Transform(entity);
            if (xform.ParentUid != uid)
                continue;

            _xformSystem.AttachToGridOrMap(entity, xform);

            if (TryComp<VentCrawlerComponent>(entity, out var ventCrawComp))
            {
                ventCrawComp.InTube = false;
                Dirty(entity, ventCrawComp);
            }

            if (TryComp<PhysicsComponent>(entity, out var physics))
                _physicsSystem.WakeBody(entity, body: physics);
        }

        PredictedQueueDel(uid);
    }

    /// <summary>
    /// Updates entities with <seealso cref="VentCrawlHolderComponent"/> and processes their movement in vents.
    /// </summary>
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VentCrawlHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            if (holder.CurrentTube == null)
                continue;

            if (!Exists(holder.CurrentTube.Value) || Terminating(holder.CurrentTube.Value))
            {
                ExitVentCrawl(uid, holder);
                continue;
            }

            if (holder.IsExitingVentCrawls)
                continue;

            if (_gameTiming.CurTime < holder.ManifoldTransitionEnd)
                UpdateManifoldPositionInterpolated(holder.CurrentTube.Value, uid, holder);

            if (!_gameTiming.IsFirstTimePredicted)
                continue;

            if (holder.NextTube != null)
                UpdatePosition(uid, holder);

            if (!UpdateMovementInput(holder.CurrentTube.Value, uid, holder))
                continue;

            if (holder.NextTube != null && _gameTiming.CurTime >= holder.MoveEndTime)
                TryAdvanceTube(uid, holder);
        }
    }

    private void UpdatePosition(EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (holder.NextTube == null)
            return;

        var totalSeconds = (holder.MoveEndTime - holder.MoveStartTime).TotalSeconds;
        if (totalSeconds <= 0)
            return;

        var elapsed = (_gameTiming.CurTime - holder.MoveStartTime).TotalSeconds;
        var progress = (float)Math.Clamp(elapsed / totalSeconds, 0.0, 1.0);

        var worldPos = Vector2.Lerp(holder.MoveFromWorldPos, holder.MoveToWorldPos, progress);
        _xformSystem.SetWorldPosition(uid, worldPos);
    }

    private void TryAdvanceTube(EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (!TryComp<VentCrawlerComponent>(holder.ContainedEntity, out var crawler))
        {
            ExitVentCrawl(uid);
            return;
        }
        if (holder.NextTube == null || holder.NextTube == holder.CurrentTube)
        {
            holder.NextTube = null;
            holder.MoveStartTime = TimeSpan.Zero;
            holder.MoveEndTime = TimeSpan.Zero;
            return;
        }
        if (_gameTiming.CurTime > holder.LastCrawl + TimeSpan.FromSeconds(crawler.Speed))
        {
            holder.LastCrawl = _gameTiming.CurTime;
            _audioSystem.PlayPredicted(crawler.CrawlSound, uid, holder.ContainedEntity);
        }

        var nextTube = holder.NextTube.Value;

        holder.NextTube = null;
        holder.MoveStartTime = TimeSpan.Zero;
        holder.MoveEndTime = TimeSpan.Zero;
        Dirty(uid, holder);

        EnterTube(uid, nextTube, holder);

        if (!holder.IsMoving || holder.CurrentDirection == Direction.Invalid)
            return;

        var chainedNext = NextTubeFor(nextTube, holder.CurrentDirection);
        if (chainedNext == null || holder.CurrentTube == chainedNext)
            return;

        BeginMoveTo(uid, chainedNext.Value, holder.MoveToWorldPos, holder, crawler.Speed);
    }
}
