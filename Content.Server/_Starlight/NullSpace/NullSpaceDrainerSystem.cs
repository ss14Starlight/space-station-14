using Content.Server._Starlight.Shadekin;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.NullSpace;

public sealed class NullSpaceDrainerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<NullSpaceDrainerComponent, OnAttemptEnergyUseEvent>(OnAttempt);

        SubscribeLocalEvent<NullSpaceDrainerComponent, ResearchServerGetPointsPerSecondEvent>(OnGetPointsPerSecond);

        SubscribeLocalEvent<NullSpaceDrainerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NullSpaceDrainerComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnAttempt(EntityUid uid, NullSpaceDrainerComponent component, CancellableEntityEventArgs args)
    {
        _popup.PopupEntity(Loc.GetString("shadekin-fail-generic"), uid, uid, PopupType.LargeCaution);
        args.Cancel();
    }

    private void OnEquipped(EntityUid uid, NullSpaceDrainerComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EnsureComp<NullSpaceDrainerComponent>(args.Equipee);
        component.Target = args.Equipee;
    }

    private void OnUnequipped(EntityUid uid, NullSpaceDrainerComponent component, GotUnequippedEvent args)
    {
        RemComp<NullSpaceDrainerComponent>(args.Equipee);
        component.Target = null;
    }

    // You fucking monster... coding this makes me sad for my kins.
    private void OnGetPointsPerSecond(EntityUid uid, NullSpaceDrainerComponent component, ref ResearchServerGetPointsPerSecondEvent args)
    {
        if (component.Target is not null && TryComp<BrighteyeComponent>(component.Target.Value, out var brighteye) && brighteye.Energy > 0)
        {
            brighteye.Energy -= 1;
            args.Points += 50;
            Dirty(component.Target.Value, brighteye);
        }
    }
}