using Content.Shared.Popups;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.Weapons.Ranged.Events;

using Content.Shared._Starlight.Antags.Vampires.Components.Classes;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public sealed class GargantuaBloodSwellSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActiveBloodSwellComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<ActiveBloodSwellComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!TryComp<GargantuaComponent>(ent.Owner, out var gargantua))
            return;

        TryShowPopup((ent.Owner, gargantua), args.Used);
        args.Cancel();
    }

    private void TryShowPopup(Entity<GargantuaComponent> ent, EntityUid used)
    {
        if (!_net.IsClient || !_timing.IsFirstTimePredicted)
            return;

        if (used == ent.Comp.BloodSwellShootLastGun
            && ent.Comp.BloodSwellShootNextPopupTime is { } next
            && !(_timing.CurTime > next))
            return;

        ent.Comp.BloodSwellShootLastGun = used;
        ent.Comp.BloodSwellShootNextPopupTime = _timing.CurTime + ent.Comp.BloodSwellShootPopupCooldown;
        _popup.PopupClient(Loc.GetString("vampire-blood-swell-cancel-shoot"), ent.Owner, ent.Owner);
    }
}
