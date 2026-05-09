using System.Linq;
using Content.Shared.VentCrawl.Tube.Components;
using Content.Shared.VentCrawl.Components;
using Content.Shared.VentCrawl;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Containers;

namespace Content.Server.VentCrawl;

public sealed class VentCrawlableSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlHolderComponent, VentCrawlExitEvent>(OnVentCrawlExitEvent);
    }

    /// <summary>
    /// Exits the vent craws for the specified VentCrawlHolderComponent, removing it and any contained entities from the craws.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawlHolderComponent.</param>
    /// <param name="holder">The VentCrawlHolderComponent instance.</param>
    /// <param name="holderTransform">The TransformComponent instance for the VentCrawlHolderComponent.</param>
    private void OnVentCrawlExitEvent(EntityUid uid, VentCrawlHolderComponent holder, ref VentCrawlExitEvent args)
    {
        var holderTransform = args.holderTransform;

        if (Terminating(uid))
            return;

        if (!Resolve(uid, ref holderTransform))
            return;

        if (holder.IsExitingVentCrawls)
        {
            Log.Error("Tried exiting VentCrawls twice. This should never happen.");
            return;
        }

        holder.IsExitingVentCrawls = true;

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
                Dirty(entity , ventCrawComp);
            }

            if (EntityManager.TryGetComponent(entity, out PhysicsComponent? physics))
            {
                _physicsSystem.WakeBody(entity, body: physics);
            }
        }

        EntityManager.DeleteEntity(uid);
    }
}
