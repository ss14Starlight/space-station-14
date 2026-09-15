#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Starlight.Kitchen;

/// <summary>
/// Integration tests for predicted slicing/refining verb interactions.
/// </summary>
[TestFixture]
public sealed class SlicingVerbTest : InteractionTest
{
    private async Task<NetEntity> SpawnDeadPig()
    {
        var pig = await SpawnTarget("MobPig");
        await Server.WaitPost(() =>
        {
            var uid = SEntMan.GetEntity(pig);
            SEntMan.System<MobStateSystem>().ChangeMobState(uid, MobState.Dead);
        });
        await RunTicks(10);
        return pig;
    }

    [Test]
    public async Task RefineVerbAppearsWithSlicingTool()
    {
        var pig = await SpawnDeadPig();
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        await Client.WaitPost(() =>
        {
            var verbs = CEntMan.System<Content.Client.Verbs.VerbSystem>()
                .GetLocalVerbs(ToClient(pig), CPlayer, typeof(InteractionVerb));
            var verb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("refined-slice-verb-name"));

            Assert.That(verb, Is.Not.Null);
            Assert.That(verb!.Disabled, Is.False);
        });
    }

    [Test]
    public async Task RefineVerbDisabledWithoutSlicingTool()
    {
        var pig = await SpawnDeadPig();
        await DeleteHeldEntity();
        await RunTicks(10);

        await Client.WaitPost(() =>
        {
            var target = ToClient(pig);
            var verbs = CEntMan.System<Content.Client.Verbs.VerbSystem>()
                .GetLocalVerbs(target, CPlayer, typeof(InteractionVerb));
            var verb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("refined-slice-verb-name"));

            Assert.That(verb, Is.Not.Null);
            Assert.That(verb!.Disabled, Is.True);
            Assert.That(verb.Message,
                Is.EqualTo(Loc.GetString("refined-slice-verb-message-tool", ("target", target))));
        });
    }

    [Test]
    public async Task RefineVerbDisabledForLivingMob()
    {
        var pig = await SpawnTarget("MobPig");
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        await Client.WaitPost(() =>
        {
            var verbs = CEntMan.System<Content.Client.Verbs.VerbSystem>()
                .GetLocalVerbs(ToClient(pig), CPlayer, typeof(InteractionVerb));
            var verb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("refined-slice-verb-name"));

            Assert.That(verb, Is.Not.Null);
            Assert.That(verb!.Disabled, Is.True);
            Assert.That(verb.Message, Is.EqualTo(Loc.GetString("refined-slice-verb-target-isnt-dead")));
        });
    }

    [Test]
    public async Task RefineVerbEnabledOnJumpsuit()
    {
        var jumpsuit = await SpawnTarget("ClothingUniformJumpsuitColorGrey");
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        await Client.WaitPost(() =>
        {
            var verbs = CEntMan.System<Content.Client.Verbs.VerbSystem>()
                .GetLocalVerbs(ToClient(jumpsuit), CPlayer, typeof(InteractionVerb));
            var verb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("refined-slice-verb-name"));

            Assert.That(verb, Is.Not.Null);
            Assert.That(verb!.Disabled, Is.False);
        });
    }

    [Test]
    public async Task RefineMultipleTimesSuccessfully()
    {
        var firstPig = await SpawnDeadPig();
        await PlaceInHands("KitchenKnife");
        await Interact();
        AssertDeleted(firstPig);

        var secondPig = await SpawnDeadPig();
        await Interact();
        AssertDeleted(secondPig);
    }

    [Test]
    public async Task CancelledRefineDoesNotDeleteTarget()
    {
        var pig = await SpawnDeadPig();
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        await Interact(awaitDoAfters: false);
        await CancelDoAfters();
        await RunSeconds(1.5f);

        AssertExists(pig);

        await Interact(awaitDoAfters: true);
        AssertDeleted(pig);
    }
}

/// <summary>
/// Verifies that a slicing quality built into the user is accepted without a held tool.
/// </summary>
[TestFixture]
public sealed class InbuiltSlicingVerbTest : InteractionTest
{
    [TestPrototypes]
    private const string SlicingPlayerPrototype = """
    -   type: entity
        parent: InteractionTestMob
        id: InteractionTestSlicingMob
        components:
        -   type: Tool
            qualities:
                - Slicing
    """;

    protected override string PlayerPrototype => "InteractionTestSlicingMob";

    [Test]
    public async Task RefineVerbEnabledWithInbuiltSlicingTool()
    {
        var pig = await SpawnTarget("MobPig");
        await Server.WaitPost(() =>
        {
            var uid = SEntMan.GetEntity(pig);
            SEntMan.System<MobStateSystem>().ChangeMobState(uid, MobState.Dead);
        });
        await RunTicks(10);

        await Client.WaitPost(() =>
        {
            var verbs = CEntMan.System<Content.Client.Verbs.VerbSystem>()
                .GetLocalVerbs(ToClient(pig), CPlayer, typeof(InteractionVerb));
            var verb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("refined-slice-verb-name"));

            Assert.That(verb, Is.Not.Null);
            Assert.That(verb!.Disabled, Is.False);
        });
    }
}
