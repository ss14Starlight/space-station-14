using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Weapons.Ranged.Systems;

// Randomizes shots-per-burst each time a burst completes for guns with GunBurstVarianceDefectComponent.
public sealed partial class GunBurstVarianceSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    /// <summary>
    /// Salt for the random number generator to ensure that the randomization is different each time a burst completes.
    /// </summary>
    private const int BurstSalt = -5;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<GunBurstVarianceDefectComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.CurrentShots = _random.Next(ent.Comp.MinShots, ent.Comp.MaxShots + 1);
        Dirty(ent, ent.Comp);

        if (HasComp<GunComponent>(ent.Owner))
            _gun.RefreshModifiers(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnRefreshModifiers(Entity<GunBurstVarianceDefectComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.CurrentShots <= 0)
            return;

        args.ShotsPerBurst = ent.Comp.CurrentShots;
    }

    [SubscribeLocalEvent]
    private void OnGunShot(Entity<GunBurstVarianceDefectComponent> ent, ref GunShotEvent args)
    {
        if (!TryComp<GunComponent>(ent.Owner, out var gun))
            return;

        if (gun.SelectedMode != SelectiveFire.Burst || gun.BurstActivated)
            return;

        ent.Comp.CurrentShots = _gun.GetShotRandom(ent, BurstSalt).Next(ent.Comp.MinShots, ent.Comp.MaxShots + 1);
        Dirty(ent, ent.Comp);
        _gun.RefreshModifiers(ent.Owner);
    }
}
