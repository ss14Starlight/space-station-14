using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Xenobiology;

public sealed class ClawSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClawComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClawComponent, AfterInteractEvent>(OnAfterInteract);
    }
    
    private void OnMapInit(Entity<ClawComponent> entity, ref MapInitEvent args)
    {
        entity.Comp.Container = _containerSystem.EnsureContainer<ContainerSlot>(entity.Owner, entity.Comp.ClawContainerId);
        Dirty(entity.Owner, entity.Comp);
    }

    private void OnAfterInteract(Entity<ClawComponent> entity, ref AfterInteractEvent args)
    {
        entity.Comp.Container = _containerSystem.EnsureContainer<ContainerSlot>(entity.Owner, entity.Comp.ClawContainerId);
        if (entity.Comp.Container.Count <= 0)
        {
            if (!args.Target.HasValue) return;
            var canReach = _interactionSystem.InRangeUnobstructed(args.User, args.Target.Value, entity.Comp.ClawInteractionRange, CollisionGroup.None);
            if (!canReach) return;
            if (_whitelist.IsWhitelistFail(entity.Comp.AllowedEntities, args.Target.Value)) return;
            _containerSystem.Insert(args.Target.Value, entity.Comp.Container);
        }
        else
        {
            var canReach = _interactionSystem.InRangeUnobstructed(args.User, args.ClickLocation, entity.Comp.ClawInteractionRange, CollisionGroup.None);
            if (!canReach) return;
            _containerSystem.Remove(entity.Comp.Container.ContainedEntities[0], entity.Comp.Container, true, false,
                args.ClickLocation);
        }
    }
}