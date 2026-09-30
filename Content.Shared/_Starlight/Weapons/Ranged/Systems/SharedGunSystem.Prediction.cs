using System.Numerics;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Projectiles;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private const int RecoilSalt = -1;
    private const int PelletSalt = -2;

    public int GetShotSeed(EntityUid gun, int salt = 0)
        => SharedRandomExtensions.HashCodeCombine((int) Timing.CurTick.Value, GetNetEntity(gun).Id, salt);

    public int GetHitscanSeed(EntityUid gun, int ammoIndex, int pelletIndex)
        => GetShotSeed(gun, SharedRandomExtensions.HashCodeCombine(ammoIndex, pelletIndex));

    private System.Random GetShotRandom(EntityUid gun, int salt)
        => Random.GetPredictedRandom(Timing, GetShotSeed(gun, salt));

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

        var max = (float) spread.Deviation.Theta;

        // Every pellet strays up to Deviation either way, but never leaves the spread cone.
        for (var i = 0; i < spread.Count; i++)
        {
            var theta = (start + ((end - start) * i / (spread.Count - 1))).Theta;
#pragma warning disable CS0618
            theta += random.NextFloat(-max, max);
#pragma warning restore CS0618
            angles[i] = new Angle(Math.Clamp(theta, start.Theta, end.Theta));
        }

        return angles;
    }
}
