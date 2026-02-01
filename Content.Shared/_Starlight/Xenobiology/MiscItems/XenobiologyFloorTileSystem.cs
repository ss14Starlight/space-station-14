using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Starlight.Xenobiology.MiscItems;

public sealed class XenobiologyFloorTileSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stackSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobiologyFloorTileComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<XenobiologyFloorTileComponent> entity, ref AfterInteractEvent args)
    {
        if (!_stackSystem.TryUse(entity.Owner, 1)) return;
        var location = args.ClickLocation.AlignWithClosestGridTile();
        PredictedSpawnAtPosition(entity.Comp.Entity.Id, location);
    }
}