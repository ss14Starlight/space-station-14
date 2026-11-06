// ReSharper disable once CheckNamespace
using Content.Shared.Mech.Components;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

// Helper for enumerating all held guns
public abstract partial class SharedGunSystem
{
    /// <summary>
    /// Enumerates all guns held by the given entity. Similar to <see cref="TryGetGun(EntityUid, out Entity{GunComponent})"/> but yields all held guns instead of just the "next" one.
    /// </summary>
    public IEnumerable<Entity<GunComponent>> EnumerateGuns(EntityUid entity)
    {
        GunComponent? gunComp;

        // guns in your hand
        foreach (var held in _hands.EnumerateHeld(entity))
        {
            if (TryComp(held, out gunComp))
                yield return (held, gunComp);
        }

        // guns in the mech (you are piloting the mech)
        if (TryComp<MechPilotComponent>(entity, out var pilot) &&
            TryComp(pilot.Mech, out MechComponent? mechFromPilot) &&
            mechFromPilot.CurrentSelectedEquipment is { } equipFromPilot &&
            TryComp(equipFromPilot, out gunComp))
            yield return (equipFromPilot, gunComp);

        // guns in the mech (you are the mech)
        if (TryComp<MechComponent>(entity, out var mech) &&
            mech.CurrentSelectedEquipment is { } equip &&
            TryComp(equip, out gunComp))
            yield return (equip, gunComp);

        // the gun (you are the gun)
        if (TryComp(entity, out gunComp))
            yield return (entity, gunComp);
    }
}
