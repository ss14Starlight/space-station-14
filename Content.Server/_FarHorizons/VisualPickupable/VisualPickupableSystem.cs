using Content.Shared._FarHorizons.VisualPickupable;
using Content.Shared.Hands;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.VisualPickupable;

public sealed class VisualPickupableSystem : SharedVisualPickupableSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly EntProtoId _cloneEnt = "VisualPickupableCloneEntity";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisualPickupableComponent, GotEquippedHandEvent>(OnGotPickedUp);
        SubscribeLocalEvent<VisualPickupableComponent, GotUnequippedHandEvent>(OnGotDropped);
    }

    private void OnGotPickedUp(Entity<VisualPickupableComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.ClonedVisuals != null) return;

        var clone = SpawnAttachedTo(_cloneEnt, Transform(args.User).Coordinates);
        _transform.SetParent(clone, args.User);
        ent.Comp.ClonedVisuals = clone;

        var cloneComp = EnsureComp<PickupableVisualsComponent>(clone);
        cloneComp.Source = ent;
        Dirty<PickupableVisualsComponent>((clone, cloneComp));
    }

    private void OnGotDropped(Entity<VisualPickupableComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (args.Handled ||
            ent.Comp.ClonedVisuals == null) return;

        Del(ent.Comp.ClonedVisuals);
        ent.Comp.ClonedVisuals = null;
    }
}
