using Content.Server.Explosion.EntitySystems;
using Content.Shared._Starlight.Defects.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Defects.Systems;

/// <summary>
/// Triggers a localised explosion at the gun's position on a per-shot roll.
/// The explosion is centred on the weapon tile so it naturally damages anyone
/// standing there, including the shooter.
/// </summary>
public sealed partial class BackfireDefectSystem : EntitySystem
{
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BackfireDefectComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<BackfireDefectComponent> ent, ref GunShotEvent args)
    {
        if (!_random.Prob(ent.Comp.BackfireChance))
            return;

        _explosion.QueueExplosion(
            ent.Owner,
            ent.Comp.ExplosionTypeId,
            ent.Comp.TotalIntensity,
            ent.Comp.IntensitySlope,
            ent.Comp.MaxIntensity,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false,
            user: args.User);
    }
}
