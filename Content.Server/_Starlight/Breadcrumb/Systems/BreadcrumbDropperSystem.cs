using System.Linq;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared._Starlight.Breadcrumb.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Breadcrumb.Systems;

public sealed partial class BreadcrumbDropperSystem : SharedBreadcrumbDropperSystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SharedPointLightSystem _pointLight = null!;
    [Dependency] private ExpendableLightSystem _expendableLight = null!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var droppers = EntityQueryEnumerator<BreadcrumbDropperComponent, TransformComponent>();
        while (droppers.MoveNext(out var uid, out var dropperComp, out var xform))
        {
            if (!dropperComp.Enabled ||
                now < dropperComp.NextDropCheck)
                continue;

            dropperComp.NextDropCheck = now + dropperComp.DropCheckFrequency;

            Entity<BreadcrumbDropperComponent> dropper = (uid, dropperComp);
            if (HasDroppingNearby(dropper, xform))
                continue;

            if (!ChargesSystem.TryUseCharge(uid))
                continue; // TODO: sound or smth?

            Drop(dropper, xform);
        }
    }

    private bool HasDroppingNearby(Entity<BreadcrumbDropperComponent> dropper, TransformComponent xform)
        => _lookup
            .GetEntitiesInRange<BreadcrumbDroppingComponent>(xform.Coordinates, dropper.Comp.DropRange)
            .Any(ent => ent.Comp.Dropper == dropper.Owner);

    private void Drop(Entity<BreadcrumbDropperComponent> dropper, TransformComponent xform)
    {
        var ent = SpawnAtPosition(dropper.Comp.DroppingPrototype, xform.Coordinates);

        var dropping = EnsureComp<BreadcrumbDroppingComponent>(ent);
        dropping.Dropper = dropper.Owner;
        dropping.Color = dropper.Comp.Color;

        var phase = dropper.Comp.NextIndex++ * dropper.Comp.IndexPhaseMultiplier;
        dropping.PhaseOffset = phase - MathF.Truncate(phase); // Strips whole numbers, leaves the decimal.

        Dirty(ent, dropping);

        _pointLight.SetColor(ent, dropping.Color);
        _appearance.SetData(ent, BreadcrumbDroppingVisualLayers.Color, dropping.Color);

        // TODO: play pop sound?
    }

    [SubscribeLocalEvent]
    public void OnStartup(Entity<BreadcrumbDroppingComponent> dropping, ref ComponentStartup ev)
    {
        //_lightBehavior.SetOffset(dropping.Owner, dropping.Comp.PhaseOffset)
        if (TryComp<ExpendableLightComponent>(dropping, out var expendableLight))
            _expendableLight.TryActivate((dropping, expendableLight));
    }

}
