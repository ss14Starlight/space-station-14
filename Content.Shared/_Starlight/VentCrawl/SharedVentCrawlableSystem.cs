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

namespace Content.Shared.VentCrawl;

/// <summary>
/// A system that handles the crawling behavior for vent creatures.
/// </summary>
public sealed class SharedVentCrawlableSystem : EntitySystem
{
    [Dependency] private readonly SharedVentTubeSystem _ventCrawTubeSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlHolderComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VentCrawlHolderComponent, MoveInputEvent>(OnMoveInput);
    }

    /// <summary>
    /// Handles the MoveInputEvent for VentCrawlHolderComponent.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawlHolderComponent.</param>
    /// <param name="component">The VentCrawlHolderComponent instance.</param>
    /// <param name="args">The MoveInputEvent arguments.</param>
    private void OnMoveInput(EntityUid uid, VentCrawlHolderComponent holder, ref MoveInputEvent args)
    {

        if (!EntityManager.EntityExists(holder.CurrentTube))
        {
            var ev = new VentCrawlExitEvent();
            RaiseLocalEvent(uid, ref ev);
        }

        holder.IsMoving = args.State;
        holder.CurrentDirection = args.Dir;
    }

    /// <summary>
    /// Handles the ComponentStartup event for VentCrawlHolderComponent.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawlHolderComponent.</param>
    /// <param name="holder">The VentCrawlHolderComponent instance.</param>
    /// <param name="args">The ComponentStartup arguments.</param>
    private void OnComponentStartup(EntityUid uid, VentCrawlHolderComponent holder, ComponentStartup args)
    {
        holder.Container = _containerSystem.EnsureContainer<Container>(uid, nameof(VentCrawlHolderComponent));
    }

    /// <summary>
    /// Tries to insert an entity into the VentCrawlHolderComponent container.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawlHolderComponent.</param>
    /// <param name="toInsert">The EntityUid of the entity to insert.</param>
    /// <param name="holder">The VentCrawlHolderComponent instance.</param>
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
    /// Checks whether the specified entity can be inserted into the container of the VentCrawlHolderComponent.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawlHolderComponent.</param>
    /// <param name="toInsert">The EntityUid of the entity to be inserted.</param>
    /// <param name="holder">The VentCrawlHolderComponent instance.</param>
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
    /// Attempts to make the VentCrawlHolderComponent enter a VentCrawlTubeComponent.
    /// </summary>
    /// <param name="holderUid">The EntityUid of the VentCrawlHolderComponent.</param>
    /// <param name="toUid">The EntityUid of the VentCrawlTubeComponent to enter.</param>
    /// <param name="holder">The VentCrawlHolderComponent instance.</param>
    /// <param name="holderTransform">The TransformComponent instance for the VentCrawlHolderComponent.</param>
    /// <param name="to">The VentCrawlTubeComponent instance to enter.</param>
    /// <param name="toTransform">The TransformComponent instance for the VentCrawlTubeComponent.</param>
    /// <returns>True if the VentCrawlHolderComponent successfully enters the VentCrawlTubeComponent; otherwise, False.</returns>
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
            var ev = new VentCrawlExitEvent();
            RaiseLocalEvent(holderUid, ref ev);
            return false;
        }

        foreach (var ent in holder.Container.ContainedEntities)
        {
            var comp = EnsureComp<BeingVentCrawlComponent>(ent);
            comp.Holder = holderUid;
        }

        if (!_containerSystem.Insert(holderUid, to.Contents))
        {
            var ev = new VentCrawlExitEvent();
            RaiseLocalEvent(holderUid, ref ev);
            return false;
        }
        if (TryComp<PhysicsComponent>(holderUid, out var physBody))
            _physicsSystem.SetCanCollide(holderUid, false, body: physBody);

        if (holder.CurrentTube != null)
        {
            holder.PreviousTube = holder.CurrentTube;
            holder.PreviousDirection = holder.CurrentDirection;
        }
        holder.CurrentTube = toUid;

        return true;
    }

    /// <summary>
    ///  Magic...
    /// </summary>
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VentCrawlHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            if (holder.CurrentDirection == Direction.Invalid || holder.CurrentTube == null)
                continue;

            var currentTube = holder.CurrentTube.Value;

            if (holder.IsMoving && holder.NextTube == null)
            {
                var nextTube = _ventCrawTubeSystem.NextTubeFor(currentTube, holder.CurrentDirection);

                if (nextTube != null)
                {
                    if (!EntityManager.EntityExists(holder.CurrentTube))
                    {
                        var ev = new VentCrawlExitEvent();
                        RaiseLocalEvent(uid, ref ev);
                        continue;
                    }

                    holder.NextTube = nextTube;
                    holder.StartingTime = holder.Speed;
                    holder.TimeLeft = holder.Speed;
                }
                else
                {
                    var ev = new GetVentCrawlsConnectableDirectionsEvent();
                    RaiseLocalEvent(currentTube, ref ev);
                    if (ev.Connectable.Contains(holder.CurrentDirection))
                    {
                        var Exitev = new VentCrawlExitEvent();
                        RaiseLocalEvent(uid, ref Exitev);
                        continue;
                    }
                }
            }

            if (holder.NextTube != null && holder.TimeLeft > 0)
            {
                var time = frameTime;
                if (time > holder.TimeLeft)
                {
                    time = holder.TimeLeft;
                }

                var progress = 1 - holder.TimeLeft / holder.StartingTime;
                var origin = Transform(currentTube).Coordinates;
                var target = Transform(holder.NextTube.Value).Coordinates;
                var newPosition = (target.Position - origin.Position) * progress;

                _xformSystem.SetCoordinates(uid, origin.Offset(newPosition).WithEntityId(currentTube));

                holder.TimeLeft -= time;
                frameTime -= time;
            }
            else if (holder.NextTube != null && holder.TimeLeft == 0)
            {
                var welded = false;
                if (TryComp<WeldableComponent>(holder.NextTube.Value, out var weldableComponent))
                    welded = weldableComponent.IsWelded;
                if (HasComp<VentCrawlEntryComponent>(holder.NextTube.Value) && !holder.FirstEntry && !welded)
                {
                    var ev = new VentCrawlExitEvent();
                    RaiseLocalEvent(uid, ref ev);
                }
                else
                {
                    _containerSystem.Remove(uid, Comp<VentCrawlTubeComponent>(currentTube).Contents ,reparent: false, force: true);

                    if (holder.FirstEntry)
                        holder.FirstEntry = false;

                    if (_gameTiming.CurTime > holder.LastCrawl + VentCrawlHolderComponent.CrawlDelay)
                    {
                        holder.LastCrawl = _gameTiming.CurTime;
                        _audioSystem.PlayPvs(holder.CrawlSound, uid);
                    }

                    EnterTube(uid, holder.NextTube.Value, holder);
                    holder.NextTube = null;
                }
            }
        }
    }
}
