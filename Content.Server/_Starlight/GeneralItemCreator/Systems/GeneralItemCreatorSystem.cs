using Content.Server.Ninja.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared._Starlight.GeneralItemCreator.Systems;
using Content.Shared._Starlight.GeneralItemCreator.Components;

namespace Content.Server._Starlight.GeneralItemCreator.Systems;

// It's the same as the ninja one but without a Ninja check.
public sealed partial class GeneralItemCreatorSystem : SharedGeneralItemCreatorSystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GeneralItemCreatorComponent, GeneralCreateItemEvent>(OnCreateItem);
        SubscribeLocalEvent<GeneralItemCreatorComponent, NinjaBatteryChangedEvent>(OnBatteryChanged);
    }

    private void OnCreateItem(Entity<GeneralItemCreatorComponent> ent, ref GeneralCreateItemEvent args)
    {
        var (uid, comp) = ent;
        if (comp.Battery is not { } battery)
            return;

        args.Handled = true;

        var user = args.Performer;
        if (!_battery.TryUseCharge(battery, comp.Charge))
        {
            _popup.PopupEntity(Loc.GetString(comp.NoPowerPopup), user, user);
            return;
        }

        // try to put item in hand, otherwise it goes on the ground
        var star = Spawn(comp.SpawnedPrototype, Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, star);
    }

    private void OnBatteryChanged(Entity<GeneralItemCreatorComponent> ent, ref NinjaBatteryChangedEvent args)
    {
        if (ent.Comp.Battery == args.Battery)
            return;

        var comp = ent.Comp;
        comp.Battery = args.Battery;
        Dirty(ent, comp);
    }
}
