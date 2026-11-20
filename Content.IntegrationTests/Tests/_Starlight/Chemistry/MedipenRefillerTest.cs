#nullable enable
using System.Threading.Tasks;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Starlight.Chemistry.Components;
using Content.Shared._Starlight.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Chemistry;

[TestFixture]
public sealed class MedipenRefillerTest : InteractionTest
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    private SharedSolutionContainerSystem Solutions => SEntMan.System<SharedSolutionContainerSystem>();

    private Solution Tank(EntityUid refiller)
    {
        Assert.That(Solutions.TryGetSolution(refiller, "tank", out _, out var tank));
        return tank!;
    }

    private void Fill(EntityUid refiller, string reagent, FixedPoint2 amount)
    {
        Assert.That(Solutions.TryGetSolution(refiller, "tank", out var soln, out _));
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

    private async Task<(Entity<MedipenRefillerComponent> Refiller, MedipenRefillerSystem System)> PoweredRefiller()
    {
        await SpawnTarget("MedipenRefiller");
        ToggleNeedPower();
        await RunTicks(5);
        var refiller = STarget!.Value;
        return ((refiller, SEntMan.GetComponent<MedipenRefillerComponent>(refiller)), SEntMan.System<MedipenRefillerSystem>());
    }

    [Test]
    public async Task RefillsAnEmptyPenFromItsTank()
    {
        await PoweredRefiller();
        var pen = ToServer(await PlaceInHands("EmergencyMedipen"));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(pen);
            Assert.That(SEntMan.System<TagSystem>().HasTag(pen, TrashTag), "an empty pen counts as trash");
            Fill(STarget!.Value, "Epinephrine", 20);
            Fill(STarget.Value, "TranexamicAcid", 10);
        });

        await Interact();
        await Server.WaitAssertion(() =>
        {
            var solution = Pen(pen);
            Assert.That(solution.GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(solution.GetTotalPrototypeQuantity("TranexamicAcid"), Is.EqualTo(FixedPoint2.New(5)));
            var tank = Tank(STarget!.Value);
            Assert.That(tank.GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.New(5)), "the whole dose is paid for");
            Assert.That(tank.GetTotalPrototypeQuantity("TranexamicAcid"), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(SEntMan.System<TagSystem>().HasTag(pen, TrashTag), Is.False, "a refilled pen isn't trash");
        });
    }

    [Test]
    public async Task RefusesFullUnlistedAndShortCases()
    {
        var (refiller, system) = await PoweredRefiller();
        var full = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));
        var unlisted = await SpawnEntity("EmergencyMedipenMute", SEntMan.GetCoordinates(TargetCoords));
        var empty = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            Fill(refiller, "Epinephrine", 100);
            Fill(refiller, "TranexamicAcid", 4);
            Fill(refiller, "MuteToxin", 50);
            EmptyPen(unlisted);
            EmptyPen(empty);

            Assert.That(system.TryRefill(refiller, full, out _), Is.False, "a full pen is refused");
            Assert.That(system.TryRefill(refiller, unlisted, out _), Is.False, "a pen off the list is refused");
            Assert.That(system.TryRefill(refiller, empty, out _), Is.False, "4u of tranexamic acid is short of 5u");
            Assert.That(Pen(empty).Volume, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Tank(refiller).GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.New(100)),
                "a refused refill takes nothing");

            Fill(refiller, "TranexamicAcid", 1);
            Assert.That(system.TryRefill(refiller, empty, out _), "with the whole dose in the tank it refills");
        });
    }

    [Test]
    public async Task RefusesWithoutPower()
    {
        await SpawnTarget("MedipenRefiller");
        var pen = await SpawnEntity("EmergencyMedipen", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            var refiller = STarget!.Value;
            Fill(refiller, "Epinephrine", 20);
            Fill(refiller, "TranexamicAcid", 10);
            EmptyPen(pen);
            var comp = SEntMan.GetComponent<MedipenRefillerComponent>(refiller);
            Assert.That(SEntMan.System<MedipenRefillerSystem>().TryRefill((refiller, comp), pen, out _), Is.False);
            Assert.That(Pen(pen).Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task PutsBackSpriteFillerForFree()
    {
        var (refiller, system) = await PoweredRefiller();
        var pen = await SpawnEntity("PunctAutoInjector", SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            EmptyPen(pen);
            Fill(refiller, "Puncturase", 12);
            Fill(refiller, "TranexamicAcid", 5);

            Assert.That(system.TryRefill(refiller, pen, out _));
            Assert.That(Pen(pen).Volume, Is.EqualTo(FixedPoint2.New(20)), "the pen gets its whole original fill");
            Assert.That(Tank(refiller).Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }
}
