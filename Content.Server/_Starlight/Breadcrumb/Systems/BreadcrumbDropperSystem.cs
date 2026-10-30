using System.Linq;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared._Starlight.Breadcrumb.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
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
            if (HasCrumbNearby(dropper, xform))
                continue;

            if (!ChargesSystem.TryUseCharge(uid))
            {
                dropperComp.Enabled = false;
                PopupSystem.PopupEntity(Loc.GetString("breadcrumb-dropper-emptied"), dropper, PopupType.Medium);
                Dirty(dropper);
                continue;
            }

            Drop(dropper, xform);
        }
    }

    private bool HasCrumbNearby(Entity<BreadcrumbDropperComponent> dropper, TransformComponent xform)
    {
        foreach (var ent in _lookup
                     .GetEntitiesInRange<BreadcrumbComponent>(xform.Coordinates, dropper.Comp.DropRange))
        {
            if (ent.Comp.Dropper != dropper.Owner)
                continue;

            if (!TryComp<ExpendableLightComponent>(ent, out var expendableLight) ||
                expendableLight.CurrentState == ExpendableLightState.Dead)
                continue;

            return true;
        }

        return false;
    }

    private void Drop(Entity<BreadcrumbDropperComponent> dropper, TransformComponent xform)
    {
        var ent = SpawnAtPosition(dropper.Comp.BreadcrumbPrototype, xform.Coordinates);

        var crumb = EnsureComp<BreadcrumbComponent>(ent);
        crumb.Dropper = dropper.Owner;
        crumb.Color = dropper.Comp.Color;
        Dirty(ent, crumb);

        _pointLight.SetColor(ent, crumb.Color);
        _appearance.SetData(ent, BreadcrumbVisualData.Color, crumb.Color);
    }

    [SubscribeLocalEvent]
    public void OnBreadcrumbMapInit(Entity<BreadcrumbComponent> crumb, ref MapInitEvent ev)
    {
        if (TryComp<ExpendableLightComponent>(crumb, out var expendableLight))
            _expendableLight.TryActivate((crumb, expendableLight));
    }
}
