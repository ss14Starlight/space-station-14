using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.VentCrawl.Tube.Components;
using Content.Shared.VentCrawl.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Actions;

namespace Content.Shared.VentCrawl.EntitySystems;

/// <summary>
/// A system that handles the crawling behavior for vent creatures.
/// </summary>
public sealed partial class SharedVentCrawlableSystem : EntitySystem
{
    [Dependency] private SharedVentCrawlTubeSystem _ventCrawlTubeSystem = default!;
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlHolderComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VentCrawlHolderComponent, MoveInputEvent>(OnMoveInput);

        SubscribeLocalEvent<BeingVentCrawlComponent, ExitVentActionEvent>(OnExitVentActionEvent);
    }

    /// <summary>
    /// Handles the MoveInputEvent for <seealso cref="VentCrawlHolderComponent"/>.
    /// </summary>
    private void OnMoveInput(EntityUid uid, VentCrawlHolderComponent holder, ref MoveInputEvent args)
    {

        if (!Exists(holder.CurrentTube))
        {
            ExitVentCrawl(uid);
            return;
        }

        holder.IsMoving = args.State;
        holder.CurrentDirection = args.Dir;
        DirtyField(uid, holder, nameof(VentCrawlHolderComponent.CurrentDirection));
    }

    /// <summary>
    /// Handles the ComponentStartup event for <seealso cref="VentCrawlHolderComponent"/>.
    /// </summary>
    private void OnComponentStartup(EntityUid uid, VentCrawlHolderComponent holder, ComponentStartup args)
        => holder.Container = _containerSystem.EnsureContainer<Container>(uid, nameof(VentCrawlHolderComponent));

    /// <summary>
    /// Tries to insert an entity into the <seealso cref="VentCrawlHolderComponent"/> container.
    /// </summary>
    /// <returns>True if the insertion was successful, otherwise False.</returns>
    public bool TryInsert(EntityUid uid, EntityUid toInsert, VentCrawlHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder))
            return false;

        if (!CanInsert(uid, toInsert, holder))
            return false;

        if (!_containerSystem.Insert(toInsert, holder.Container))
            return false;

        if (TryComp<PhysicsComponent>(toInsert, out var physBody))
            _physicsSystem.SetCanCollide(toInsert, false, body: physBody);

        return true;
    }

    /// <summary>
    /// Checks whether the specified entity can be inserted into the container of the <seealso cref="VentCrawlHolderComponent"/>.
    /// </summary>
    /// <returns>True if the entity can be inserted into the container; otherwise, False.</returns>
    private bool CanInsert(EntityUid uid, EntityUid toInsert, VentCrawlHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder))
            return false;

        if (!_containerSystem.CanInsert(toInsert, holder.Container))
            return false;

        return HasComp<ItemComponent>(toInsert) ||
            HasComp<BodyComponent>(toInsert);
    }

    /// <summary>
    /// Attempts to make the <seealso cref="VentCrawlHolderComponent"/> enter a <seealso cref="VentCrawlTubeComponent"/>.
    /// </summary>
    /// <returns>True if the <seealso cref="VentCrawlHolderComponent"/> successfully enters the <seealso cref="VentCrawlTubeComponent"/>; otherwise, False.</returns>
    public bool EnterTube(EntityUid holderUid, EntityUid toUid, VentCrawlHolderComponent? holder = null, TransformComponent? holderTransform = null, VentCrawlTubeComponent? to = null, TransformComponent? toTransform = null)
    {
        if (!Resolve(holderUid, ref holder, ref holderTransform))
            return false;
        if (holder.IsExitingVentCrawls)
        {
            Log.Error("Tried entering tube after exiting VentCrawls. This should never happen.");
            return false;
        }
        if (!Resolve(toUid, ref to, ref toTransform))
        {
            Log.Error("Entity without TransformComponent tried entering tube! This should never happen.");
            return false;
        }

        foreach (var ent in holder.Container.ContainedEntities)
        {
            var comp = EnsureComp<BeingVentCrawlComponent>(ent);
            comp.Holder = holderUid;
        }

        if (!_containerSystem.Insert(holderUid, to.Contents))
        {
            Log.Error("Entity tried entering tube but container system can't insert it into tube! This should never happen.");
            return false;
        }
        if (TryComp<PhysicsComponent>(holderUid, out var physBody))
            _physicsSystem.SetCanCollide(holderUid, false, body: physBody);

        if (holder.CurrentTube != null)
        {
            holder.PreviousTube = holder.CurrentTube;
            holder.PreviousDirection = holder.CurrentDirection;
            DirtyField(holderUid, holder, nameof(VentCrawlHolderComponent.PreviousTube));
            DirtyField(holderUid, holder, nameof(VentCrawlHolderComponent.PreviousDirection));
        }
        holder.CurrentTube = toUid;
        DirtyField(holderUid, holder, nameof(VentCrawlHolderComponent.CurrentTube));

        return true;
    }

    private void OnExitVentActionEvent(EntityUid uid, BeingVentCrawlComponent component, ExitVentActionEvent args)
        => ExitVentCrawl(component.Holder);

    /// <summary>
    /// Exits the vent craws for the specified <seealso cref="VentCrawlHolderComponent"/>, removing it and any contained entities from the craws.
    /// </summary>
    public void ExitVentCrawl(EntityUid uid, VentCrawlHolderComponent? holder = null, TransformComponent? holderTransform = null)
    {
        if (Terminating(uid) || !Resolve(uid, ref holder, ref holderTransform, false))
            return;

        if (holder.IsExitingVentCrawls)
        {
            Log.Error("Tried exiting VentCrawls twice. This should never happen.");
            return;
        }

        holder.IsExitingVentCrawls = true;

        if (holder.HasExitAction)
        {
            foreach (var action in holder.ProvidedActions)
                _actionsSystem.RemoveAction(action);

            holder.ProvidedActions.Clear();
            holder.HasExitAction = false;
        }

        foreach (var entity in holder.Container.ContainedEntities.ToArray())
        {
            RemComp<BeingVentCrawlComponent>(entity);

            var meta = MetaData(entity);
            _containerSystem.Remove(entity, holder.Container, reparent: false, force: true);

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
            {
                _physicsSystem.WakeBody(entity, body: physics);
            }
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

            var currentTube = holder.CurrentTube.Value;

            if (!UpdateMovementInput(currentTube, uid, holder))
                continue;

            if (holder.NextTube != null)
            {
                holder.TimeLeft -= frameTime;

                if (holder.TimeLeft > 0)
                    UpdatePosition(currentTube, uid, holder, frameTime);
                else
                    TryAdvanceTube(currentTube, uid, holder);
            }
        }
    }

    private bool UpdateMovementInput(EntityUid currentTube, EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (holder.CurrentDirection == Direction.Invalid)
            return true;

        if (holder.IsMoving && holder.NextTube == null)
        {
            var nextTube = _ventCrawlTubeSystem.NextTubeFor(currentTube, holder.CurrentDirection);

            if (nextTube != null)
            {
                if (!Exists(holder.CurrentTube))
                {
                    ExitVentCrawl(uid);
                    return false;
                }

                holder.NextTube = nextTube;
                DirtyField(uid, holder, nameof(VentCrawlHolderComponent.NextTube));
                holder.StartingTime = holder.TravelDuration;
                holder.TimeLeft = holder.TravelDuration;
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

    private void UpdatePosition(EntityUid currentTube, EntityUid uid, VentCrawlHolderComponent holder, float frameTime)
    {
        if (holder.NextTube == null || holder.StartingTime <= 0f)
            return;

        if (holder.CurrentDirection == Direction.Invalid)
            return;

        var progress = 1f - (holder.TimeLeft / holder.StartingTime);
        progress = Math.Clamp(progress, 0f, 1f);

        var origin = Transform(currentTube).Coordinates;
        var destination = holder.CurrentDirection.ToVec();
        var newPosition = destination * progress;

        _xformSystem.SetCoordinates(uid, _xformSystem.WithEntityId(origin.Offset(newPosition), currentTube));
    }

    private void TryAdvanceTube(EntityUid currentTube, EntityUid uid, VentCrawlHolderComponent holder)
    {
        if (holder.NextTube == null)
            return;

        var welded = false;
        if (TryComp<WeldableComponent>(holder.NextTube.Value, out var weldableComponent))
            welded = weldableComponent.IsWelded;

        if (TryComp<VentCrawlTubeComponent>(currentTube, out var tubeComp) && tubeComp.Contents.ContainedEntities.Contains(uid))
            _containerSystem.Remove(uid, tubeComp.Contents, reparent: false, force: true);

        var isValidExit = HasComp<VentCrawlEntryComponent>(holder.NextTube.Value) && !welded;

        if (isValidExit && !holder.HasExitAction)
        {
            foreach (var entity in holder.Container.ContainedEntities)
            {
                var action = _actionsSystem.AddAction(entity, holder.ActionProto);
                if (action != null)
                    holder.ProvidedActions.Add(action.Value);
            }

            holder.HasExitAction = true;
        }
        else if (!isValidExit && holder.HasExitAction)
        {
            foreach (var action in holder.ProvidedActions)
                _actionsSystem.RemoveAction(action);

            holder.ProvidedActions.Clear();
            holder.HasExitAction = false;
        }

        if (_gameTiming.CurTime > holder.LastCrawl + VentCrawlHolderComponent.CrawlDelay)
        {
            holder.LastCrawl = _gameTiming.CurTime;
            _audioSystem.PlayPvs(holder.CrawlSound, uid);
        }

        var nextTube = holder.NextTube.Value;

        holder.NextTube = null;
        holder.StartingTime = 0f;
        holder.TimeLeft = 0f;
        DirtyField(uid, holder, nameof(VentCrawlHolderComponent.NextTube));

        EnterTube(uid, nextTube, holder);
    }
}
