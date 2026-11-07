using System.Collections.Generic;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared._Starlight.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Weapons;

[TestOf(typeof(SharedGunHeatSystem))]
public sealed class GunHeatTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    private static readonly EntProtoId _gun = "WeaponDisabler";

    [Test]
    public async Task HeatThresholdsAreOrdered()
        => await Server.WaitAssertion(() =>
        {
            var bad = new List<string>();
            foreach (var proto in ProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.TryComp<GunHeatComponent>(out var heat, Factory))
                    continue;

                if (!(heat.GlowTemperature < heat.JamTemperature && heat.JamTemperature < heat.MeltTemperature))
                    bad.Add($"{proto.ID}: glow {heat.GlowTemperature}, jam {heat.JamTemperature}, melt {heat.MeltTemperature}");
                else if (heat.MaxJamChance is < 0f or > 1f)
                    bad.Add($"{proto.ID}: max jam chance {heat.MaxJamChance}");
                else if (heat.HeatPerShot <= 0f || heat.PassiveCooling < 0f)
                    bad.Add($"{proto.ID}: heat per shot {heat.HeatPerShot}, passive cooling {heat.PassiveCooling}");
            }

            Assert.That(bad, Is.Empty, $"Guns with broken heat settings:\n{string.Join("\n", bad)}");
        });

    [Test]
    public async Task ShootingHeatsAndJamBlocksFiring()
    {
        await AddAtmosphere();

        var gunNet = await PlaceInHands(_gun);
        var gun = ToServer(gunNet);
        var heat = Comp<GunHeatComponent>(gunNet);

        await Pair.RunSeconds(2f);

        var startTemperature = heat.Temperature;
        await AttemptShoot(TargetCoords);
        Assert.That(heat.Temperature, Is.GreaterThan(startTemperature), "Firing did not heat the barrel.");

        await Server.WaitPost(() =>
        {
            heat.Jammed = true;
            SEntMan.Dirty(gun, heat);
        });
        await Pair.RunSeconds(1f);

        var ammo = SGun.GetAmmoCount(gun);
        await AttemptShoot(TargetCoords, assert: false);
        Assert.That(SGun.GetAmmoCount(gun), Is.EqualTo(ammo), "A jammed gun still fired.");

        await UseInHand();
        Assert.That(heat.Jammed, Is.False, "Using the gun in hand did not clear the jam.");

        await Pair.RunSeconds(1f);
        await AttemptShoot(TargetCoords);
        Assert.That(SGun.GetAmmoCount(gun), Is.LessThan(ammo), "The gun did not fire after the jam was cleared.");
    }
}
