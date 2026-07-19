using System.Linq;
using Content.Server._Starlight.Medical.Surgery;
using Content.Shared._Starlight;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Sol.Medical.Surgery;

[TestFixture]
[TestOf(typeof(SurgerySystem))]
public sealed class SolContextualSurgeryTest
{
    [Test]
    public async Task SurgeryUiUsesContextualBui()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.TryGetMapping(typeof(EntityPrototype), "BasePart", out var basePart), Is.True);
            Assert.That(basePart!.ToString(), Does.Contain("SolContextualSurgeryBui"));

            Assert.That(proto.TryGetMapping(typeof(EntityPrototype), "MobHuman", out var human), Is.True);
            Assert.That(human!.ToString(), Does.Contain("SolContextualSurgeryBui"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClampedBleedersStopIncisionBleedTick()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman");
            var body = entMan.System<SharedBodySystem>();
            var torso = body.GetBodyChildrenOfType(patient, BodyPartType.Torso).First().Id;

            var incision = entMan.EnsureComponent<IncisionOpenComponent>(torso);
#pragma warning disable RA0002 // test setup: force incision bleed tick due
            incision.NextUpdate = TimeSpan.Zero;
#pragma warning restore RA0002

            Assert.That(entMan.TryGetComponent(patient, out BloodstreamComponent blood), Is.True);
            var beforeUnclamped = blood.BleedAmount;
            entMan.System<SurgerySystem>().Update(1f);
            var afterUnclamped = blood.BleedAmount;
            Assert.That(afterUnclamped, Is.GreaterThan(beforeUnclamped));

            entMan.EnsureComponent<BleedersClampedComponent>(torso);
#pragma warning disable RA0002
            incision.NextUpdate = TimeSpan.Zero;
#pragma warning restore RA0002
            entMan.System<SurgerySystem>().Update(1f);
            Assert.That(blood.BleedAmount, Is.EqualTo(afterUnclamped));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BoneGelConsumesContainerSolution()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman");
            var surgeon = entMan.Spawn("MobHuman");
            var body = entMan.System<SharedBodySystem>();
            var torso = body.GetBodyChildrenOfType(patient, BodyPartType.Torso).First().Id;
            var gel = entMan.Spawn("BoneGel");
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            var entities = entMan.System<StarlightEntitySystem>();

            Assert.That(solutions.TryGetSolution(gel, "container", out _), Is.True);
            var before = solutions.GetTotalPrototypeQuantity(gel, "BoneGel");
            Assert.That(before, Is.GreaterThanOrEqualTo(FixedPoint2.New(5)));

            Assert.That(entities.TryGetSingleton("SurgeryStepMendRibcage", out var stepEnt), Is.True);

            var ev = new SurgeryStepEvent(surgeon, patient, torso, [gel])
            {
                StepProto = "SurgeryStepMendRibcage",
                SurgeryProto = "SurgeryCloseIncision",
            };
            entMan.EventBus.RaiseLocalEvent(stepEnt, ref ev);

            var after = solutions.GetTotalPrototypeQuantity(gel, "BoneGel");
            Assert.That(after, Is.EqualTo(before - FixedPoint2.New(5)));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GetNextStepTraversesRequirements()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.Spawn("MobHuman");
            Assert.That(entMan.System<StandingStateSystem>().Down(patient), Is.True);

            var bodySys = entMan.System<SharedBodySystem>();
            var torso = bodySys.GetBodyChildrenOfType(patient, BodyPartType.Torso).First().Id;
            var surgerySys = entMan.System<SharedSurgerySystem>();
            var entities = entMan.System<StarlightEntitySystem>();

            Assert.That(entities.TryGetSingleton("SurgeryCloseIncision", out var surgery), Is.True);
            var next = surgerySys.GetNextStep(patient, torso, surgery);
            Assert.That(next, Is.Not.Null);

            var nextProto = entMan.GetComponent<MetaDataComponent>(next!.Value.Surgery.Owner).EntityPrototype?.ID;
            Assert.That(nextProto, Is.EqualTo("SurgeryOpenIncision"));
            Assert.That(next.Value.Step, Is.EqualTo(0));
        });

        await pair.CleanReturnAsync();
    }
}
