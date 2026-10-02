using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Starlight.Wieldable;

public sealed partial class WieldOrderSystem : EntitySystem
{
    [Dependency] private SharedWieldableSystem _wieldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, UseInHandEvent>(OnUseInHand,
            before: [typeof(SharedWieldableSystem), typeof(SharedGunSystem)]);
    }

    private void OnUseInHand(Entity<GunComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled
            || !TryComp<WieldableComponent>(ent, out var wieldable)
            || wieldable.Wielded)
            return;

        if (!TryComp<WieldOrderComponent>(args.User, out var order) || !order.WieldBeforeRack)
            return;

        // If wielding is not possible right now, fall through so the bolt still gets racked.
        args.Handled = _wieldable.TryWield(ent, wieldable, args.User);
    }
}
