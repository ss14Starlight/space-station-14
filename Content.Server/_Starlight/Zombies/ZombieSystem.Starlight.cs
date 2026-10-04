using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Zombies;

// ReSharper disable once CheckNamespace
namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{
    partial void OnDeadTargetInfectionFailed(Entity<ZombieComponent> entity, EntityUid uid, ref MeleeHitEvent args)
    {
        if (!_mobState.IsDead(uid))
            return;

        if (!HasComp<ZombieImmuneComponent>(uid) && GetZombieInfectionChance(uid, entity.Comp) > 0f)
            return;

        _popup.PopupEntity(Loc.GetString("zombie-bite-infection-immune"), entity, entity);
    }
}
