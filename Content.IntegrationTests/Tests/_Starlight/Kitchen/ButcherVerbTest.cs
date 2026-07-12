#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Verbs;
using Content.Shared._Starlight.Kitchen.EntitySystems;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Starlight.Kitchen;

/// <summary>
///     Integration tests for predicted butcher verb menu interactions.
/// </summary>
[TestFixture]
public sealed class ButcherVerbTest : InteractionTest
{
    /// <summary>
    ///     Spawns a dead pig entity for testing butchering.
    /// </summary>
    private async Task<NetEntity> SpawnDeadPig()
    {
        var pig = await SpawnTarget("MobPig");
        await Server.WaitPost(() =>
        {
            var mobStateSystem = SEntMan.System<MobStateSystem>();
            mobStateSystem.ChangeMobState(SEntMan.GetEntity(pig), MobState.Dead);

            if (SEntMan.TryGetComponent<ButcherableComponent>(SEntMan.GetEntity(pig), out var butcher))
            {
                butcher.ButcherDelay = 0.01f;
            }
        });
        await RunTicks(10);
        return pig;
    }

    /// <summary>
    ///     Verifies that the butcher verb is predicted on the client and appears in local verbs when holding a knife.
    /// </summary>
    [Test]
    public async Task ButcherVerbAppearsInLocalVerbs()
    {
        // Spawn a dead pig and hold a knife
        var pig = await SpawnDeadPig();
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        // Get local verbs on client to check if they are predicted
        await Client.WaitPost(() =>
        {
            var verbSystem = CEntMan.System<Content.Client.Verbs.VerbSystem>();

            // Get the player EntityUid and client target
            var clientPlayer = CPlayer;
            var clientTarget = ToClient(pig);

            // Get local verbs (this raises the local/predicted GetVerbsEvent client-side)
            var verbs = verbSystem.GetLocalVerbs(clientTarget, clientPlayer, typeof(InteractionVerb));

            // Assert: Butcher verb is present and enabled
            var butcherVerb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("butcherable-verb-name"));
            Assert.That(butcherVerb, Is.Not.Null, "Butcher verb should be available");
            Assert.That(butcherVerb!.Disabled, Is.False, "Butcher verb should be enabled when holding a knife");
        });
    }

    /// <summary>
    ///     Verifies that the butcher verb is disabled when the player is not holding a knife.
    /// </summary>
    [Test]
    public async Task ButcherVerbDisabledWithoutKnife()
    {
        // Spawn a dead pig but keep hands empty
        var pig = await SpawnDeadPig();
        await DeleteHeldEntity();
        await RunTicks(10);

        // Get local verbs on client to check if disabled
        await Client.WaitPost(() =>
        {
            var verbSystem = CEntMan.System<Content.Client.Verbs.VerbSystem>();

            var clientPlayer = CPlayer;
            var clientTarget = ToClient(pig);

            var verbs = verbSystem.GetLocalVerbs(clientTarget, clientPlayer, typeof(InteractionVerb));

            // Assert: Butcher verb is present but disabled
            var butcherVerb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("butcherable-verb-name"));
            Assert.That(butcherVerb, Is.Not.Null, "Butcher verb should be available");
            Assert.That(butcherVerb!.Disabled, Is.True, "Butcher verb should be disabled when not holding a knife");
            Assert.That(butcherVerb.Message, Is.EqualTo(Loc.GetString("butcherable-need-knife", ("target", clientTarget))),
                "Disabled message should specify that a knife is needed");
        });
    }

    /// <summary>
    ///     Verifies that the butcher verb is enabled when the player has an inbuilt sharp component directly on themselves.
    /// </summary>
    [Test]
    public async Task ButcherVerbEnabledWithInbuiltSharp()
    {
        // Spawn a dead pig with empty hands, but give the player the sharp component directly
        var pig = await SpawnDeadPig();
        await DeleteHeldEntity();

        // Add SharpComponent to player on the server
        await Server.WaitPost(() =>
        {
            SEntMan.AddComponent<SharpComponent>(SEntMan.GetEntity(Player));
        });
        await RunTicks(10);

        // Check if the verb is enabled on the client
        await Client.WaitPost(() =>
        {
            var verbSystem = CEntMan.System<Content.Client.Verbs.VerbSystem>();

            var clientPlayer = CPlayer;
            var clientTarget = ToClient(pig);

            var verbs = verbSystem.GetLocalVerbs(clientTarget, clientPlayer, typeof(InteractionVerb));

            // Assert: Butcher verb is present and enabled
            var butcherVerb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("butcherable-verb-name"));
            Assert.That(butcherVerb, Is.Not.Null, "Butcher verb should be available");
            Assert.That(butcherVerb!.Disabled, Is.False, "Butcher verb should be enabled when the player has an inbuilt sharp component");
        });

        // Cleanup: remove SharpComponent from player for subsequent tests
        await Server.WaitPost(() =>
        {
            SEntMan.RemoveComponent<SharpComponent>(SEntMan.GetEntity(Player));
        });
        await RunTicks(10);
    }

    /// <summary>
    ///     Verifies that the butcher verb is disabled when the target is inside a container.
    /// </summary>
    [Test]
    public async Task ButcherVerbDisabledInsideContainer()
    {
        // Spawn a dead pig and hold a knife
        var pig = await SpawnDeadPig();
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        // Server-side: create a container and insert the dead pig
        await Server.WaitPost(() =>
        {
            var containerEnt = SEntMan.SpawnEntity("LockerSteel", SEntMan.GetCoordinates(TargetCoords));
            var containerSystem = SEntMan.System<SharedContainerSystem>();
            var container = containerSystem.EnsureContainer<Container>(containerEnt, "test_container");
            Assert.That(containerSystem.Insert(SEntMan.GetEntity(pig), container), Is.True);
        });

        // Sync state to client
        await RunTicks(10);

        // Check if the verb is disabled since it is in a container
        await Client.WaitPost(() =>
        {
            var verbSystem = CEntMan.System<Content.Client.Verbs.VerbSystem>();

            var clientPlayer = CPlayer;
            var clientTarget = ToClient(pig);

            var verbs = verbSystem.GetLocalVerbs(clientTarget, clientPlayer, typeof(InteractionVerb), force: true);

            // Assert: Butcher verb is present but disabled
            var butcherVerb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("butcherable-verb-name"));
            Assert.That(butcherVerb, Is.Not.Null, "Butcher verb should be available");
            Assert.That(butcherVerb!.Disabled, Is.True, "Butcher verb should be disabled when the target is in a container");
            Assert.That(butcherVerb.Message, Is.EqualTo(Loc.GetString("butcherable-not-in-container", ("target", clientTarget))));
        });
    }

    /// <summary>
    ///     Verifies that the butcher verb is disabled when the target is a living mob.
    /// </summary>
    [Test]
    public async Task ButcherVerbDisabledLivingMob()
    {
        // Spawn a living pig and hold a knife
        var pig = await SpawnTarget("MobPig");
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        // Check if verb is disabled because target isn't dead
        await Client.WaitPost(() =>
        {
            var verbSystem = CEntMan.System<Content.Client.Verbs.VerbSystem>();

            var clientPlayer = CPlayer;
            var clientTarget = ToClient(pig);

            var verbs = verbSystem.GetLocalVerbs(clientTarget, clientPlayer, typeof(InteractionVerb));

            // Assert: Butcher verb is present but disabled
            var butcherVerb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("butcherable-verb-name"));
            Assert.That(butcherVerb, Is.Not.Null, "Butcher verb should be available");
            Assert.That(butcherVerb!.Disabled, Is.True, "Butcher verb should be disabled when the target is a living mob");
            Assert.That(butcherVerb.Message, Is.EqualTo(Loc.GetString("butcherable-mob-isnt-dead")));
        });
    }

    /// <summary>
    ///     Verifies that the butcher verb is enabled immediately for non-mob items like a jumpsuit.
    /// </summary>
    [Test]
    public async Task ButcherVerbEnabledOnJumpsuit()
    {
        // Spawn a jumpsuit and hold a knife
        var jumpsuit = await SpawnTarget("ClothingUniformJumpsuitColorGrey");
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        // Check if the verb is enabled on the client
        await Client.WaitPost(() =>
        {
            var verbSystem = CEntMan.System<Content.Client.Verbs.VerbSystem>();

            var clientPlayer = CPlayer;
            var clientTarget = ToClient(jumpsuit);

            var verbs = verbSystem.GetLocalVerbs(clientTarget, clientPlayer, typeof(InteractionVerb));

            var butcherVerb = verbs.FirstOrDefault(v => v.Text == Loc.GetString("butcherable-verb-name"));
            Assert.That(butcherVerb, Is.Not.Null, "Butcher verb should be available on the jumpsuit");
            Assert.That(butcherVerb!.Disabled, Is.False, "Butcher verb should be enabled for a jumpsuit");
        });
    }

    /// <summary>
    ///     Verifies that multiple butchering attempts can be performed sequentially in the same test session.
    /// </summary>
    [Test]
    public async Task ButcherMultipleTimesSuccessfully()
    {
        // Spawn first dead pig and hold a knife
        var pig1 = await SpawnDeadPig();
        await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        // Interact to start butchering, and wait for do-after to complete
        await Interact();

        // Assert pig1 is deleted
        AssertDeleted(pig1);

        // Spawn a second dead pig
        var pig2 = await SpawnDeadPig();

        // Interact with the second pig
        await Interact();

        // Assert pig2 is deleted
        AssertDeleted(pig2);
    }

    /// <summary>
    ///     Verifies that cancelling a butchering do-after cleans up internal state and doesn't delete the entity after the delay.
    /// </summary>
    [Test]
    public async Task ButcherCancelledCleansUp()
    {
        // Spawn a dead pig and hold a knife
        var pig = await SpawnDeadPig();

        // Override delay to 0.5s so it doesn't complete during the 1 tick in Interact()
        await Server.WaitPost(() =>
        {
            if (SEntMan.TryGetComponent<ButcherableComponent>(SEntMan.GetEntity(pig), out var butcher))
            {
                butcher.ButcherDelay = 0.5f;
            }
        });

        var knife = await PlaceInHands("KitchenKnife");
        await RunTicks(10);

        // Start butchering, but don't await do-after completion immediately
        await Interact(awaitDoAfters: false);

        // Cancel the do-after
        await CancelDoAfters();

        // Wait for the duration of the cancelled do-after to ensure it doesn't trigger anyway
        var sharpSystem = SEntMan.System<SharedSharpSystem>();
        var delay = sharpSystem.GetButcherDelay(SEntMan.GetEntity(knife), SEntMan.GetEntity(pig));
        await RunSeconds(delay + 1f);

        // Assert that the pig is NOT deleted after the duration has elapsed
        AssertExists(pig);

        // Verify we can successfully butcher the pig on the second attempt
        await Interact(awaitDoAfters: true);
        AssertDeleted(pig);
    }
}
