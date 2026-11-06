using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared._Starlight.Medical.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Medical;

[TestFixture]
public sealed class RegenerativeSleepTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> _bluntDamageType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> _piercingDamageType = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> _bloodlossDamageType = "Bloodloss";

    [Test]
    public async Task HealsDamageAndBleedingOnlyWhileSleeping()
    {
        var pair = Pair;
        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var damageable = server.System<DamageableSystem>();
        var bloodstream = server.System<BloodstreamSystem>();
        var sleeping = server.System<SleepingSystem>();
        EntityUid felionoid = default;

        try
        {
            await server.WaitPost(() =>
            {
                felionoid = entityManager.SpawnEntity("MobFelionoid", MapCoordinates.Nullspace);
            });
            await pair.RunTicksSync(5);

            var blunt = prototypes.Index(_bluntDamageType);
            var piercing = prototypes.Index(_piercingDamageType);
            var bloodloss = prototypes.Index(_bloodlossDamageType);

            await server.WaitPost(() =>
            {
                var damage = new DamageSpecifier(blunt, FixedPoint2.New(4))
                             + new DamageSpecifier(piercing, FixedPoint2.New(4))
                             + new DamageSpecifier(bloodloss, FixedPoint2.New(4));
                damageable.TryChangeDamage(felionoid, damage, true);
                bloodstream.TryModifyBleedAmount(felionoid, 4f);
            });

            await pair.RunTicksSync(5);

            await server.WaitPost(() =>
            {
                using (Assert.EnterMultipleScope())
                {
                    AssertDamage(damageable, felionoid, blunt, 4);
                    AssertDamage(damageable, felionoid, piercing, 4);
                    AssertDamage(damageable, felionoid, bloodloss, 4);
                    Assert.That(entityManager.GetComponent<RegenerativeSleepComponent>(felionoid).BleedHealPerUpdate,
                        Is.EqualTo(0.5f));
                }

                Assert.That(sleeping.TrySleeping(felionoid), Is.True);
            });

            await pair.RunTicksSync(2);

            await server.WaitPost(() =>
            {
                using (Assert.EnterMultipleScope())
                {
                    AssertDamage(damageable, felionoid, blunt, 3.5f);
                    AssertDamage(damageable, felionoid, piercing, 3);
                    AssertDamage(damageable, felionoid, bloodloss, 3);
                    Assert.That(entityManager.GetComponent<BloodstreamComponent>(felionoid).BleedAmount,
                        Is.EqualTo(3.5f));
                }
            });
        }
        finally
        {
            if (felionoid.IsValid())
            {
                await server.WaitPost(() => entityManager.DeleteEntity(felionoid));
                await pair.RunUntilSynced();
            }
        }
    }

    private static void AssertDamage(
        DamageableSystem damageable,
        EntityUid uid,
        DamageTypePrototype type,
        FixedPoint2 expected)
    {
        var damage = damageable.GetAllDamage(uid);
        Assert.That(damage.DamageDict[type.ID], Is.EqualTo(expected));
    }
}
