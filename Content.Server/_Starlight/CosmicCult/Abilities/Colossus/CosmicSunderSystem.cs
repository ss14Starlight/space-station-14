using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.CosmicCult.Abilities.Colossus;

public sealed partial class CosmicSunderSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private OccluderSystem _occluder = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicColossusComponent, EventCosmicColossusSunder>(OnColossusSunder);
    }

    private void OnColossusSunder(Entity<CosmicColossusComponent> ent, ref EventCosmicColossusSunder args)
    {
        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);

        // Range is validated by the action prototype; this only checks line of sight.
        if (!_occluder.InRangeUnoccluded(origin, target, 0f, ignoreTouching: true))
            return;

        args.Handled = true;

        var comp = ent.Comp;
        _appearance.SetData(ent, ColossusVisuals.Status, ColossusStatus.Action);
        _transform.SetCoordinates(ent, args.Target);
        _transform.AnchorEntity(ent);

        comp.Attacking = true;
        comp.AttackHoldTimer = comp.AttackWait + _timing.CurTime;
        Spawn(comp.Attack1Vfx, args.Target);

        var detonator = Spawn(comp.TileDetonations, args.Target);
        EnsureComp<CosmicTileDetonatorComponent>(detonator, out var detonateComp);
        detonateComp.DetonationTimer = _timing.CurTime;
    }
}
