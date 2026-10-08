using System.Numerics;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Projectiles;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private const int RecoilSalt = -1;
    private const int PelletSalt = -2;

    private static readonly TimeSpan MaxShotTickLag = TimeSpan.FromSeconds(0.5);

    private GameTick? _shotTick;

    public GameTick ShotTick => _shotTick ?? Timing.CurTick;

    public int GetShotSeed(EntityUid gun, int salt = 0)
        => SharedRandomExtensions.HashCodeCombine((int) ShotTick.Value, GetNetEntity(gun).Id, salt);

    private static GameTick? GetBurstShotTick(GunComponent gun)
    {
        if (!gun.BurstActivated || gun.BurstShotsCount <= 0 || gun.BurstTick == GameTick.Zero)
            return null;

        return new GameTick(gun.BurstTick.Value + (uint) gun.BurstShotsCount);
    }

    private GameTick? GetRequestTick(GameTick tick)
    {
        var now = Timing.CurTick;
        if (tick > now || tick == GameTick.Zero)
            return null;

        var maxLag = (uint) Math.Ceiling(MaxShotTickLag.TotalSeconds * Timing.TickRate);
        return now.Value - tick.Value <= maxLag ? tick : null;
    }

    public int GetHitscanSeed(EntityUid gun, int ammoIndex, int pelletIndex)
        => GetShotSeed(gun, SharedRandomExtensions.HashCodeCombine(ammoIndex, pelletIndex));

    public System.Random GetShotRandom(EntityUid gun, int salt)
        => new(GetShotSeed(gun, salt));

    public Vector2 GetShotMapDirection(Entity<GunComponent> gun, Vector2 fromMap, Vector2 toMap)
    {
        var aimed = toMap - fromMap;
        var angle = GetRecoilAngle(gun, aimed.ToAngle());

        gun.Comp.LastFire = gun.Comp.NextFire;
        DirtyField(gun.AsNullable(), nameof(GunComponent.LastFire));

        return angle.ToVec() * aimed.Length();
    }

    public Angle[] GetPelletAngles(Entity<GunComponent> gun, ProjectileSpreadComponent spread, Angle shotAngle, int ammoIndex)
    {
        var spreadEvent = new GunGetAmmoSpreadEvent(spread.Spread);
        RaiseLocalEvent(gun, ref spreadEvent);

        var start = shotAngle - (spreadEvent.Spread / 2);
        var end = shotAngle + (spreadEvent.Spread / 2);

        if (spread.Count <= 1)
            return [new Angle((start + end) / 2)];

        var random = GetShotRandom(gun, SharedRandomExtensions.HashCodeCombine(PelletSalt, ammoIndex));
        var angles = new Angle[spread.Count];
        var sector = (end - start) / spread.Count;

        // Every pellet strays up to Deviation either way, but never leaves the spread cone.
        for (var i = 0; i < spread.Count; i++)
            angles[i] = new Angle(start + (sector * (i + random.NextDouble())));

        return angles;
    }
}
