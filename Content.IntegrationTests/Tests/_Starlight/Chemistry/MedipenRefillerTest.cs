#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared._Starlight.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using NUnit.Framework;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Chemistry;

[TestFixture]
public sealed class MedipenRefillerTest : InteractionTest
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    private SharedSolutionContainerSystem Solutions => SEntMan.System<SharedSolutionContainerSystem>();
    private MedipenRefillerSystem Refillers => SEntMan.System<MedipenRefillerSystem>();

    private Solution Contents(EntityUid jug)
    {
        Assert.That(Solutions.TryGetDrainableSolution(jug, out _, out var solution));
        return solution!;
    }

    private void Fill(EntityUid jug, string reagent, FixedPoint2 amount)
    {
        Assert.That(Solutions.TryGetDrainableSolution(jug, out var soln, out _));
        Assert.That(Solutions.TryAddReagent(soln!.Value, reagent, amount), $"{amount}u of {reagent} fits");
    }

    private bool Stored(EntityUid jug) => Refillers.GetJugs(STarget!.Value).Any(stored => stored.Jug == jug);

    private Solution Pen(EntityUid pen)
    {
        Assert.That(Solutions.TryGetSolution(pen, "hypospray", out _, out var solution));
        return solution!;
    }

    private void EmptyPen(EntityUid pen)
    {
        Assert.That(Solutions.TryGetSolution(pen, "hypospray", out var soln, out _));
        Solutions.RemoveAllSolution(soln!.Value);
    }

    private EntityUid Refiller => STarget!.Value;

    private async Task PoweredRefiller()
    {
        await SpawnTarget("MedipenRefiller");
        ToggleNeedPower();
        await RunTicks(5);
    }

    /// <summary>
    /// Puts a jug of one reagent into the refiller the way a player does: click it on the machine.
    /// </summary>
    private async Task<EntityUid> LoadJug(string reagent, FixedPoint2 amount)
    {
        var jug = ToServer(await PlaceInHands("Jug"));
        await Server.WaitPost(() => Fill(jug, reagent, amount));
        await Interact();
        await Server.WaitAssertion(() => Assert.That(Stored(jug), $"the {reagent} jug went in"));
        return jug;
    }

    /// <summary>
    /// Puts a jug into the refiller directly, for tests that aren't about loading.
    /// </summary>
    private async Task<EntityUid> StoreJug(string reagent, FixedPoint2 amount)
    {
        var jug = await SpawnEntity("Jug", SEntMan.GetCoordinates(TargetCoords));
        await Server.WaitAssertion(() =>
        {
            Fill(jug, reagent, amount);
            Assert.That(SEntMan.System<SharedStorageSystem>().Insert(STarget!.Value, jug, out _, playSound: false));
        });
        return jug;
    }

    [Test]
    public async Task RefillsAnEmptyPenFromSeparateJugs()
    {
        await PoweredRefiller();
        var epinephrine = await LoadJug("Epinephrine", 20);
        var tranexamic = await LoadJug("TranexamicAcid", 10);
        var pen = ToServer(await PlaceInHands("EmergencyMedipen"));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(pen);
            Assert.That(SEntMan.System<TagSystem>().HasTag(pen, TrashTag), "an empty pen counts as trash");
        });

        await Interact();
        await Server.WaitAssertion(() =>
        {
            var solution = Pen(pen);
            Assert.That(solution.GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(solution.GetTotalPrototypeQuantity("TranexamicAcid"), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(SEntMan.System<TagSystem>().HasTag(pen, TrashTag), Is.False, "a refilled pen isn't trash");

            // Each jug paid its own reagent and nothing else went into it.
            Assert.That(Contents(epinephrine).Volume, Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(Contents(epinephrine).GetTotalPrototypeQuantity("TranexamicAcid"), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Contents(tranexamic).Volume, Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(Contents(tranexamic).GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.Zero));

            Assert.That(SEntMan.System<SharedAppearanceSystem>().TryGetData<float>(pen, SolutionContainerVisuals.FillFraction, out var fraction)
                && fraction > 0, "the pen's appearance says it's full");
        });

        // The client draws the full sprite again rather than the empty one it switched to.
        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var cPen = ToClient(pen);
            var sprite = CEntMan.GetComponent<SpriteComponent>(cPen);
            var spriteSys = CEntMan.System<SpriteSystem>();
            Assert.That(spriteSys.LayerMapTryGet((cPen, sprite), SolutionContainerLayers.Fill, out var layer, false));
            Assert.That(spriteSys.LayerGetRsiState((cPen, sprite), layer).Name, Is.EqualTo("medipen"));
        });
    }

    [Test]
    public async Task OneReagentCanComeFromSeveralJugs()
    {
        await PoweredRefiller();
        var first = await StoreJug("Epinephrine", 10);
        var second = await StoreJug("Epinephrine", 10);
        await StoreJug("TranexamicAcid", 5);
        var pen = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(pen);
            Assert.That(Refillers.TryRefill(Refiller, pen, out var reason), reason);
            Assert.That(Pen(pen).GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(Contents(first).Volume + Contents(second).Volume, Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [TestCase("EmergencyMedipen")]
    [TestCase("BruteAutoInjector")]
    public async Task RefillsAPenUsedOnSomeone(string penProto)
    {
        await SpawnTarget("MobHuman");
        var pen = ToServer(await PlaceInHands(penProto));
        await Interact();
        await RunSeconds(3);

        var refillerUid = await SpawnEntity("MedipenRefiller", SEntMan.GetCoordinates(TargetCoords));
        await Server.WaitPost(() => SEntMan.System<SharedPowerReceiverSystem>().SetNeedsPower(refillerUid, false));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Pen(pen).Volume, Is.EqualTo(FixedPoint2.Zero), "the injection empties the pen");
            foreach (var reagent in new[] { "Epinephrine", "TranexamicAcid", "Bicaridine" })
            {
                var jug = SEntMan.SpawnAtPosition("Jug", SEntMan.GetCoordinates(TargetCoords));
                Fill(jug, reagent, 30);
                Assert.That(SEntMan.System<SharedStorageSystem>().Insert(refillerUid, jug, out _, playSound: false));
            }

            var canRefill = Refillers.CanRefill(refillerUid, pen, out _, out var reason);
            Assert.That(canRefill, $"{SEntMan.ToPrettyString(pen)} refused: {reason}");
        });
    }

    [Test]
    public async Task RefusesAFullPen()
    {
        await PoweredRefiller();
        var epinephrine = await StoreJug("Epinephrine", 100);
        var tranexamic = await StoreJug("TranexamicAcid", 10);
        var full = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            // Everything the pen needs is loaded, so only the pen being full can stop it.
            Assert.That(Refillers.TryRefill(Refiller, full, out _), Is.False, "a full pen is refused");
            Assert.That(Contents(epinephrine).Volume, Is.EqualTo(FixedPoint2.New(100)), "a refused refill takes nothing");
            Assert.That(Contents(tranexamic).Volume, Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    [Test]
    public async Task RefusesAnUnlistedPen()
    {
        await PoweredRefiller();
        var mute = await StoreJug("MuteToxin", 50);
        await StoreJug("Pax", 50);
        var unlisted = await SpawnEntity("EmergencyMedipenMute", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            // Its whole dose is loaded, so only the pen list can refuse it.
            EmptyPen(unlisted);
            Assert.That(Refillers.TryRefill(Refiller, unlisted, out _), Is.False, "a pen off the list is refused");
            Assert.That(Pen(unlisted).Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Contents(mute).Volume, Is.EqualTo(FixedPoint2.New(50)));
        });
    }

    [Test]
    public async Task RefusesWhenShort()
    {
        await PoweredRefiller();
        var epinephrine = await StoreJug("Epinephrine", 100);
        await StoreJug("TranexamicAcid", 4);
        var empty = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(empty);

            Assert.That(Refillers.TryRefill(Refiller, empty, out var reason), Is.False, "4u of tranexamic acid is short of 5u");
            Assert.That(reason, Does.Contain("4"), "the popup says how much is there");
            Assert.That(Pen(empty).Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Contents(epinephrine).Volume, Is.EqualTo(FixedPoint2.New(100)), "a refused refill takes nothing");
        });

        await StoreJug("TranexamicAcid", 1);
        await Server.WaitAssertion(() =>
            Assert.That(Refillers.TryRefill(Refiller, empty, out _), "with the whole dose loaded it refills"));
    }

    [Test]
    public async Task RefusesWithoutPower()
    {
        await SpawnTarget("MedipenRefiller");
        await StoreJug("Epinephrine", 20);
        await StoreJug("TranexamicAcid", 10);
        var pen = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(pen);
            Assert.That(Refillers.TryRefill(Refiller, pen, out _), Is.False);
            Assert.That(Pen(pen).Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task PutsBackSpriteFillerForFree()
    {
        await PoweredRefiller();
        var puncturase = await StoreJug("Puncturase", 12);
        var tranexamic = await StoreJug("TranexamicAcid", 5);
        var pen = await SpawnEntity("PunctAutoInjector", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(pen);
            Assert.That(Refillers.TryRefill(Refiller, pen, out _));
            Assert.That(Pen(pen).Volume, Is.EqualTo(FixedPoint2.New(20)), "the pen gets its whole original fill");
            Assert.That(Contents(puncturase).Volume + Contents(tranexamic).Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task WindowEjectsAJugIntoTheHand()
    {
        await PoweredRefiller();
        var jug = await LoadJug("Epinephrine", 20);
        await LoadJug("TranexamicAcid", 10);

        await Activate();
        Assert.That(IsUiOpen(MedipenRefillerUiKey.Key), "using the refiller opens its window");

        ItemStorageLocation location = default;
        await Server.WaitPost(() => location = Refillers.GetJugs(STarget!.Value).First(stored => stored.Jug == jug).Location);
        await SendBui(MedipenRefillerUiKey.Key, new ReagentDispenserEjectContainerMessage(location));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Stored(jug), Is.False, "the jug came out");
            Assert.That(HandSys.IsHolding(SPlayer, jug), "into the player's hand");
            Assert.That(Refillers.GetJugs(STarget!.Value), Has.Count.EqualTo(1), "the other jug stays");
            Assert.That(Contents(jug).GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.New(20)));
        });
    }

    [Test]
    public async Task RefusesBeakersAndHoldsFourJugs()
    {
        await PoweredRefiller();
        var beaker = ToServer(await PlaceInHands("LargeBeaker"));
        await Interact();
        await Server.WaitAssertion(() => Assert.That(Stored(beaker), Is.False, "only jugs go in, like the chem dispenser"));
        await Server.WaitPost(() => SEntMan.DeleteEntity(beaker));

        for (var i = 0; i < 4; i++)
        {
            await StoreJug("Water", 10);
        }

        var fifth = await SpawnEntity("Jug", SEntMan.GetCoordinates(TargetCoords));
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedStorageSystem>().CanInsert(STarget!.Value, fifth, out _), Is.False, "a fifth jug doesn't fit"));
    }
}
