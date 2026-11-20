#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared._Starlight.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
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

    private Solution Contents(EntityUid container)
    {
        Assert.That(Solutions.TryGetFitsInDispenser(container, out _, out var solution));
        return solution!;
    }

    private void Fill(EntityUid container, string reagent, FixedPoint2 amount)
    {
        Assert.That(Solutions.TryGetFitsInDispenser(container, out var soln, out _));
        Assert.That(Solutions.TryAddReagent(soln!.Value, reagent, amount), $"{amount}u of {reagent} fits");
    }

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

    private Entity<MedipenRefillerComponent> Refiller => (STarget!.Value, SEntMan.GetComponent<MedipenRefillerComponent>(STarget.Value));

    private async Task PoweredRefiller()
    {
        await SpawnTarget("MedipenRefiller");
        ToggleNeedPower();
        await RunTicks(5);
    }

    /// <summary>
    /// Puts a beaker of one reagent into the refiller the way a player does: click it on the machine.
    /// </summary>
    private async Task<EntityUid> LoadBeaker(string reagent, FixedPoint2 amount)
    {
        var beaker = ToServer(await PlaceInHands("LargeBeaker"));
        await Server.WaitPost(() => Fill(beaker, reagent, amount));
        await Interact();
        await Server.WaitAssertion(() =>
            Assert.That(Refiller.Comp.Slots.Any(slot => SEntMan.System<ItemSlotsSystem>().GetItemOrNull(STarget!.Value, slot) == beaker),
                $"the {reagent} beaker went into a slot"));
        return beaker;
    }

    /// <summary>
    /// Puts a beaker into the refiller directly, for tests that aren't about loading.
    /// </summary>
    private async Task<EntityUid> SlotBeaker(string reagent, FixedPoint2 amount)
    {
        var beaker = await SpawnEntity("LargeBeaker", SEntMan.GetCoordinates(TargetCoords));
        await Server.WaitAssertion(() =>
        {
            Fill(beaker, reagent, amount);
            var slots = SEntMan.System<ItemSlotsSystem>();
            var free = Refiller.Comp.Slots.First(slot => slots.GetItemOrNull(STarget!.Value, slot) == null);
            Assert.That(slots.TryInsert(STarget!.Value, free, beaker, null));
        });
        return beaker;
    }

    [Test]
    public async Task RefillsAnEmptyPenFromSeparateBeakers()
    {
        await PoweredRefiller();
        var epinephrine = await LoadBeaker("Epinephrine", 20);
        var tranexamic = await LoadBeaker("TranexamicAcid", 10);
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

            // Each beaker paid its own reagent and nothing else went into it.
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
    public async Task OneReagentCanComeFromSeveralBeakers()
    {
        await PoweredRefiller();
        var first = await SlotBeaker("Epinephrine", 10);
        var second = await SlotBeaker("Epinephrine", 10);
        await SlotBeaker("TranexamicAcid", 5);
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
            var refiller = (refillerUid, SEntMan.GetComponent<MedipenRefillerComponent>(refillerUid));
            var slots = SEntMan.System<ItemSlotsSystem>();
            var reagents = new[] { "Epinephrine", "TranexamicAcid", "Bicaridine" };
            for (var i = 0; i < reagents.Length; i++)
            {
                var beaker = SEntMan.SpawnAtPosition("LargeBeaker", SEntMan.GetCoordinates(TargetCoords));
                Fill(beaker, reagents[i], 30);
                Assert.That(slots.TryInsert(refillerUid, refiller.Item2.Slots[i], beaker, null));
            }

            var canRefill = Refillers.CanRefill(refiller, pen, out _, out var reason);
            Assert.That(canRefill, $"{SEntMan.ToPrettyString(pen)} refused: {reason}");
        });
    }

    [Test]
    public async Task RefusesFullUnlistedAndShortCases()
    {
        await PoweredRefiller();
        var epinephrine = await SlotBeaker("Epinephrine", 100);
        await SlotBeaker("TranexamicAcid", 4);
        await SlotBeaker("MuteToxin", 50);
        var full = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));
        var unlisted = await SpawnEntity("EmergencyMedipenMute", SEntMan.GetCoordinates(TargetCoords));
        var empty = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(unlisted);
            EmptyPen(empty);

            Assert.That(Refillers.TryRefill(Refiller, full, out _), Is.False, "a full pen is refused");
            Assert.That(Refillers.TryRefill(Refiller, unlisted, out _), Is.False, "a pen off the list is refused");
            Assert.That(Refillers.TryRefill(Refiller, empty, out var reason), Is.False, "4u of tranexamic acid is short of 5u");
            Assert.That(reason, Does.Contain("4"), "the popup says how much is there");
            Assert.That(Pen(empty).Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Contents(epinephrine).Volume, Is.EqualTo(FixedPoint2.New(100)), "a refused refill takes nothing");
        });

        await SlotBeaker("TranexamicAcid", 1);
        await Server.WaitAssertion(() =>
            Assert.That(Refillers.TryRefill(Refiller, empty, out _), "with the whole dose loaded it refills"));
    }

    [Test]
    public async Task RefusesWithoutPower()
    {
        await SpawnTarget("MedipenRefiller");
        await SlotBeaker("Epinephrine", 20);
        await SlotBeaker("TranexamicAcid", 10);
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
        var puncturase = await SlotBeaker("Puncturase", 12);
        var tranexamic = await SlotBeaker("TranexamicAcid", 5);
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
    public async Task AltClickEjectsABeaker()
    {
        await PoweredRefiller();
        var beaker = await LoadBeaker("Epinephrine", 20);

        await Interact(altInteract: true);
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(Refiller.Comp.Slots.All(slot => slots.GetItemOrNull(STarget!.Value, slot) == null), "the beaker came out");
            Assert.That(Contents(beaker).GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.New(20)));
        });
    }
}
