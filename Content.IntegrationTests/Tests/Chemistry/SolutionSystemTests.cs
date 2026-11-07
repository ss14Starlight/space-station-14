using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;


// We are adding two non-reactive solutions in these tests
// To ensure volume(A) + volume(B) = volume(A+B)
// reactions can change this assumption
[TestFixture]
[TestOf(typeof(SharedSolutionContainerSystem))]
public sealed class SolutionSystemTests : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
    -   type: entity
        id: SolutionTarget
        components:
        -   type: Solution
            id: beaker
            solution:
                maxVol: 50
        -   type: Spillable

    -   type: reagent
        id: TestReagentA
        name: reagent-name-nothing
        desc: reagent-desc-nothing
        physicalDesc: reagent-physical-desc-nothing

    -   type: reagent
        id: TestReagentB
        name: reagent-name-nothing
        desc: reagent-desc-nothing
        physicalDesc: reagent-physical-desc-nothing

    -   type: reagent
        id: TestReagentC
        specificHeat: 2.0
        name: reagent-name-nothing
        desc: reagent-desc-nothing
        physicalDesc: reagent-physical-desc-nothing

    -   type: reagent
        id: TestReagentD
        name: reagent-name-nothing
        desc: reagent-desc-nothing
        physicalDesc: reagent-physical-desc-nothing

    -   type: reaction
        id: TestReagentA
        reactants:
            TestReagentC:
                amount: 1
            TestReagentD:
                amount: 1
        products:
            TestReagentA: 20
    ";

    private const string TestReagentA = "TestReagentA";
    private const string TestReagentB = "TestReagentB";
    private const string TestReagentC = "TestReagentC";
    private const string TestReagentD = "TestReagentD";
    private const string Water = "Water";
    private const string Oil = "Oil";

    #region Starlight
    [Test]
    public async Task TryAddNonReactiveReagentHandlesCapacity()
    {
        var pair = Pair;
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var testMap = await pair.CreateTestMap();
        var coordinates = testMap.GridCoords;

        await server.WaitAssertion(() =>
        {
            var oilQuantity = FixedPoint2.New(15);
            var waterQuantity = FixedPoint2.New(10);
            var oilAdded = new Solution(Oil, oilQuantity);
            var originalWater = new Solution(Water, waterQuantity);

            var beaker = entityManager.SpawnEntity("SolutionTarget", coordinates);
            Assert.That(containerSystem
                .TryGetSolution(beaker, "beaker", out var solutionEnt, out var solution));
            solution.AddSolution(originalWater, protoMan);
            Assert.That(containerSystem
                .TryAddSolution(solutionEnt.Value, oilAdded));

            var water = solution.GetTotalPrototypeQuantity(Water);
            var oil = solution.GetTotalPrototypeQuantity(Oil);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(water, Is.EqualTo(waterQuantity));
                Assert.That(oil, Is.EqualTo(oilQuantity));
            }

            // Adding too much reagent is rejected without changing the existing solution.
            var oversizedBeaker = entityManager.SpawnEntity("SolutionTarget", coordinates);
            Assert.That(containerSystem
                .TryGetSolution(oversizedBeaker, "beaker", out var oversizedSolutionEnt, out var oversizedSolution));
            oversizedSolution.AddSolution(new Solution(Water, waterQuantity), protoMan);
            Assert.That(containerSystem.TryAddSolution(
                oversizedSolutionEnt.Value,
                new Solution(Oil, FixedPoint2.New(1500))),
                Is.False);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(oversizedSolution.GetTotalPrototypeQuantity(Water), Is.EqualTo(waterQuantity));
                Assert.That(oversizedSolution.GetTotalPrototypeQuantity(Oil), Is.EqualTo(FixedPoint2.Zero));
            }
        });
    }
    #endregion

    [SidedDependency(Side.Server)] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    [Test]
    public async Task TryOverflowReaction()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var reagentC = new Solution(TestReagentC, 5);
            var reagentD = new Solution(TestReagentD, 5);

            var beaker = SSpawnAtPosition("SolutionTarget", testMap.GridCoords);

            Assert.That(_solutionContainer.TryGetSolution(beaker, "beaker", out var solutionEnt, out var solution));

            _solutionContainer.AddSolution(solutionEnt.Value, reagentC);
            _solutionContainer.AddSolution(solutionEnt.Value, reagentD);

            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(50)));
            var query = SEntMan.EntityQueryEnumerator<PuddleComponent>();
            Assert.That(query.MoveNext(out _), "A puddle should have been spawned from the solution");
        });
    }

    // Unlike TryAddSolution this adds and two solution without then splits leaving only threshold in original
    #region Starlight
    [Test]
    public async Task TryMixAndOverflowHandlesCapacity()
    {
        var pair = Pair;
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var testMap = await pair.CreateTestMap();
        var containerSystem = entityManager.System<SharedSolutionContainerSystem>();
        var coordinates = testMap.GridCoords;

        await server.WaitAssertion(() =>
        {
            var ratio = 9;
            var threshold = 20;
            var waterQuantity = FixedPoint2.New(10);
            var oilQuantity = FixedPoint2.New(ratio * waterQuantity.Int());
            var oilAdded = new Solution(Oil, oilQuantity);
            var originalWater = new Solution(Water, waterQuantity);

            var beaker = entityManager.SpawnEntity("SolutionTarget", coordinates);
            Assert.That(containerSystem
                .TryGetSolution(beaker, "beaker", out var solutionEnt, out var solution));
            solution.AddSolution(originalWater, protoMan);
            Assert.That(containerSystem
                .TryMixAndOverflow(solutionEnt.Value, oilAdded, threshold, out var overflowingSolution));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(threshold)));

                var waterMix = solution.GetTotalPrototypeQuantity(Water);
                var oilMix = solution.GetTotalPrototypeQuantity(Oil);
                Assert.That(waterMix, Is.EqualTo(FixedPoint2.New(threshold / (ratio + 1))));
                Assert.That(oilMix, Is.EqualTo(FixedPoint2.New(threshold / (ratio + 1) * ratio)));

                Assert.That(overflowingSolution.Volume, Is.EqualTo(FixedPoint2.New(80)));

                var waterOverflow = overflowingSolution.GetTotalPrototypeQuantity(Water);
                var oilOverFlow = overflowingSolution.GetTotalPrototypeQuantity(Oil);
                Assert.That(waterOverflow, Is.EqualTo(waterQuantity - waterMix));
                Assert.That(oilOverFlow, Is.EqualTo(oilQuantity - oilMix));
            }

            // The threshold cannot exceed the container's maximum volume.
            threshold = 60;
            oilQuantity = FixedPoint2.New(ratio * waterQuantity.Int());
            var oversizedBeaker = entityManager.SpawnEntity("SolutionTarget", coordinates);
            Assert.That(containerSystem
                .TryGetSolution(oversizedBeaker, "beaker", out var oversizedSolutionEnt, out var oversizedSolution));
            oversizedSolution.AddSolution(new Solution(Water, waterQuantity), protoMan);
            Assert.That(containerSystem
                .TryMixAndOverflow(oversizedSolutionEnt.Value, new Solution(Oil, oilQuantity), threshold, out _),
                Is.False);
        });
    }
    #endregion

    [Test]
    public async Task TestTemperatureCalculations()
    {
        var pair = Pair;
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        const float temp = 100.0f;

        // Adding reagent with adjusts temperature
        await server.WaitAssertion(() =>
        {
            var solution = new Solution(TestReagentA, FixedPoint2.New(100)) { Temperature = temp };
            Assert.That(solution.Temperature, Is.EqualTo(temp * 1));

            solution.AddSolution(new Solution(TestReagentA, FixedPoint2.New(100)) { Temperature = temp * 3 }, protoMan);
            Assert.That(solution.Temperature, Is.EqualTo(temp * 2));

            solution.AddSolution(new Solution(TestReagentB, FixedPoint2.New(100)) { Temperature = temp * 5 }, protoMan);
            Assert.That(solution.Temperature, Is.EqualTo(temp * 3));
        });

        // adding solutions combines thermal energy
        await server.WaitAssertion(() =>
        {
            var solutionOne = new Solution(TestReagentA, FixedPoint2.New(100)) { Temperature = temp };

            var solutionTwo = new Solution(TestReagentB, FixedPoint2.New(100)) { Temperature = temp };
            solutionTwo.AddReagent(TestReagentC, FixedPoint2.New(100));

            var thermalEnergyOne = solutionOne.GetHeatCapacity(protoMan) * solutionOne.Temperature;
            var thermalEnergyTwo = solutionTwo.GetHeatCapacity(protoMan) * solutionTwo.Temperature;
            solutionOne.AddSolution(solutionTwo, protoMan);
            Assert.That(solutionOne.GetHeatCapacity(protoMan) * solutionOne.Temperature, Is.EqualTo(thermalEnergyOne + thermalEnergyTwo));
        });
    }
}
