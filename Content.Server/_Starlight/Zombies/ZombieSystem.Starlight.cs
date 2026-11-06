using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Zombies;

// ReSharper disable once CheckNamespace
namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{
    partial void OnDeadTargetInfectionFailed(Entity<ZombieComponent> entity, EntityUid uid, ref MeleeHitEvent args)
    {
        if ((HasComp<BorgChassisComponent>(uid) && _mobState.IsCritical(uid)) || (_mobState.IsDead(uid) && (HasComp<ZombieImmuneComponent>(uid) || GetZombieInfectionChance(uid, entity.Comp) <= 0f)))
        {
            _popup.PopupEntity(Loc.GetString("zombie-bite-infection-immune"), entity, entity);
        }
    }
}
