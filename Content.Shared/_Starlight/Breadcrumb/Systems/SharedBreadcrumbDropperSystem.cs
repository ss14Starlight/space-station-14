using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared._Starlight.Light.Components;
using Content.Shared._Starlight.Light.EntitySystems;
using Content.Shared.Charges.Systems;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Breadcrumb.Systems;

public abstract partial class SharedBreadcrumbDropperSystem : EntitySystem
{
    [Dependency] protected SharedChargesSystem ChargesSystem = null!;
    [Dependency] protected SharedPopupSystem PopupSystem = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SharedPointLightSystem _pointLight = null!;
    [Dependency] private SharedExpendableLightSystem _expendableLight = null!;

    /// <summary>
    /// Using in hand = pressing Z. Toggle on/off.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnUseInHand(Entity<BreadcrumbDropperComponent> dropper, ref UseInHandEvent args)
    {
        dropper.Comp.Enabled = !dropper.Comp.Enabled;
        PopupSystem.PopupPredicted(
            Loc.GetString($"breadcrumb-dropper-toggle-{(dropper.Comp.Enabled ? "on" : "off")}"),
            dropper, args.User);
    }

    /// <summary>
    /// Interact using = Holding an item and then left-clicking ours. In this case we're only interested in entities
    /// that have the <see cref="BreadcrumbChargesComponent"/>.
    /// </summary>
    [SubscribeLocalEvent]
    public void OnInteractUsingEvent(Entity<BreadcrumbDropperComponent> dropper, ref InteractUsingEvent args)
    {
        if (args.Handled) return;
        if (!TryComp<BreadcrumbChargesComponent>(args.Used, out var charges))
            return;

        ChargesSystem.AddCharges((dropper, null), charges.Charges);
        QueueDel(args.Used);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var droppers = EntityQueryEnumerator<BreadcrumbDropperComponent, TransformComponent>();
        while (droppers.MoveNext(out var uid, out var dropperComp, out var xform))
        {
            if (!dropperComp.Enabled || now < dropperComp.NextDropCheck)
                continue;

            dropperComp.NextDropCheck = now + dropperComp.DropCheckFrequency;

            Entity<BreadcrumbDropperComponent> dropper = (uid, dropperComp);
            Dirty(dropper);

            if (HasCrumbNearby(dropper, xform))
                continue;

            if (!ChargesSystem.TryUseCharge(uid))
            {
                dropperComp.Enabled = false;
                PopupSystem.PopupPredicted(Loc.GetString("breadcrumb-dropper-emptied"), dropper, null, PopupType.Medium);
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
            if (ent.Comp.Dropper == dropper.Owner)
                return true;
        }

        return false;
    }

    private void Drop(Entity<BreadcrumbDropperComponent> dropper, TransformComponent xform)
    {
        var ent = PredictedSpawnAtPosition(dropper.Comp.BreadcrumbPrototype, xform.Coordinates);

        var crumb = EnsureComp<BreadcrumbComponent>(ent);
        crumb.Dropper = dropper.Owner;
        crumb.Color = dropper.Comp.Color;

        _pointLight.SetColor(ent, crumb.Color);
        // _pointLight.SetEnabled(ent, true);
        // _pointLight.SetEnergy(ent, 0f);

        _appearance.SetData(ent, BreadcrumbVisualData.Color, crumb.Color);
        Dirty(ent, crumb);

        if (TryComp<ExpendableLightComponent>(ent, out var expendableLight))
            _expendableLight.TryActivate((ent, expendableLight));
    }

    [SubscribeLocalEvent]
    public void OnBreadcrumbIgnition(Entity<BreadcrumbComponent> crumb, ref IgnitionEvent ev)
    {
        if (ev.Ignite) return;
        RemComp<BreadcrumbComponent>(crumb);
    }

    [SubscribeLocalEvent]
    public void OnBreadcrumbMapInit(Entity<BreadcrumbComponent> crumb, ref MapInitEvent ev)
    {

    }

}
