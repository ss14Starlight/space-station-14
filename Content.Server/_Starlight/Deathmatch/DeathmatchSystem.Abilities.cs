
using Content.Shared._Starlight.Deathmatch;

namespace Content.Server._Starlight.Deathmatch;

public sealed partial class DeathmatchSystem : EntitySystem
{

    public void SubscribeAbilities()
    {
        SubscribeLocalEvent<DeathmatchComponent, CreateWeaponEvent>(OnCreateWeapon);
    }
    private void OnCreateWeapon(EntityUid uid, DeathmatchComponent comp, ref CreateWeaponEvent args)
    {
        var toolbox = Spawn(args.WeaponPrototype, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, toolbox);
    }
}
