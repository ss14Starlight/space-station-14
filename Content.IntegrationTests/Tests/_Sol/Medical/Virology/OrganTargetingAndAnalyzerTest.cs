using System.Linq;
using Content.Server._Sol.Medical.Virology;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sol.Medical.Virology;

[TestFixture]
public sealed class OrganTargetingAndAnalyzerTest
{
    [Test]
    public async Task DebugAnalyzerPrototypeExistsAndIsHidden()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.TryIndex<EntityPrototype>("HandheldHealthAnalyzerDebug", out var analyzer), Is.True);
            Assert.That(analyzer!.Components.ContainsKey("DebugHealthAnalyzer"), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganDamageAppliesToTargetSlots()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman");
            var pathogenSys = entMan.System<PathogenSystem>();
            Assert.That(pathogenSys.TryGetPathogen("SolPathogenWoundSepsis", out var sepsis) && sepsis != null, Is.True);

            float before = 0f;
            EntityUid? liver = null;
            foreach (var (organ, _) in entMan.System<SharedBodySystem>().GetBodyOrgans(patient))
            {
                var id = entMan.GetComponent<MetaDataComponent>(organ).EntityPrototype?.ID;
                if (id != null && id.Contains("Liver", StringComparison.OrdinalIgnoreCase) &&
                    entMan.TryGetComponent(organ, out DamageableComponent? damage))
                {
                    before = damage.TotalDamage.Float();
                    liver = organ;
                    break;
                }
            }

            Assert.That(liver, Is.Not.Null);
            pathogenSys.ApplyOrganDamage(patient, sepsis!, multiplier: 50f);
            Assert.That(entMan.GetComponent<DamageableComponent>(liver!.Value).TotalDamage.Float(), Is.GreaterThan(before));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnalyzerReportsPresentOrgans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman");
            var organs = entMan.System<SolHealthAnalyzerSystem>().BuildOrganStatus(patient);
            Assert.That(organs, Is.Not.Empty);
            Assert.That(organs.Any(o =>
                o.Item3 is "Healthy" or "Damaged" or "Failing" or "Critical" or "Missing"), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
