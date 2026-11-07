using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Construction.Components;
using Content.Shared._Moffstation.BladeServer; // Moffstation
using Content.Shared.Construction.Components;
using Content.Shared.Lathe;
using Content.Shared.Prototypes; // Moffstation
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

public sealed class MachineBoardTest : GameTest
{
    /// <summary>
    /// A list of machine boards that can be ignored by this test.
    /// </summary>
    private readonly HashSet<string> _ignoredPrototypes = new()
    {
        //These have their own construction thing going on here
        "MachineParticleAcceleratorEndCapCircuitboard",
        "MachineParticleAcceleratorFuelChamberCircuitboard",
        "MachineParticleAcceleratorFuelChamberCircuitboard",
        "MachineParticleAcceleratorPowerBoxCircuitboard",
        "MachineParticleAcceleratorEmitterStarboardCircuitboard",
        "MachineParticleAcceleratorEmitterForeCircuitboard",
        "MachineParticleAcceleratorEmitterPortCircuitboard",
        "ParticleAcceleratorComputerCircuitboard"
    };

    #region Starlight
    /// <summary>
    /// Ensures machine and computer boards reference valid entities and component requirements.
    /// </summary>
    [Test]
    public async Task TestBoardAndResearchPrototypesAreValid()
    {
        var pair = Pair;
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFact = server.ResolveDependency<IComponentFactory>();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var p in protoMan.EnumeratePrototypes<EntityPrototype>()
                         .Where(p => !p.Abstract)
                         .Where(p => !pair.IsTestPrototype(p))
                         .Where(p => !_ignoredPrototypes.Contains(p.ID)))
            {
                if (p.TryComp<MachineBoardComponent>(out var mbc, compFact))
                {
                    var mId = mbc.Prototype;

                    Assert.Multiple(() =>
                    {
                        Assert.That(protoMan.TryIndex<EntityPrototype>(mId, out var mProto),
                            $"Machine board {p.ID}'s corresponding machine has an invalid prototype.");
                        Assert.That(mProto.TryComp<MachineComponent>(out var mComp, compFact),
                            $"Machine board {p.ID}'s corresponding machine {mId} does not have MachineComponent");
                        Assert.That(mComp.Board, Is.EqualTo(p.ID),
                            $"Machine {mId}'s BoardPrototype is not equal to it's corresponding machine board, {p.ID}");
                    });

                    Assert.Multiple(() =>
                    {
                        foreach (var component in mbc.ComponentRequirements.Keys)
                        {
                            Assert.That(entMan.ComponentFactory.TryGetRegistration(component, out _),
                                $"Invalid component requirement {component} specified on machine board entity {p}");
                        }
                    });
                }

                if (p.TryComp<ComputerBoardComponent>(out var cbc, compFact))
                {
                    var cId = cbc.Prototype;

                    Assert.Multiple(() =>
                    {
                        Assert.That(cId, Is.Not.Null, $"Computer board \"{p.ID}\" does not have a corresponding computer.");
                        Assert.That(protoMan.TryIndex<EntityPrototype>(cId, out var cProto),
                            $"Computer board \"{p.ID}\"'s corresponding computer has an invalid prototype.");
                        Assert.That(cProto.TryComp<ComputerComponent>(out var cComp, compFact),
                            $"Computer board {p.ID}'s corresponding computer \"{cId}\" does not have ComputerComponent");
                        Assert.That(cComp.BoardPrototype, Is.EqualTo(p.ID),
                            $"Computer \"{cId}\"'s BoardPrototype is not equal to it's corresponding computer board, \"{p.ID}\"");
                    });
                }
            }
        });

        // Starlight - start
        var latheSystem = entMan.System<SharedLatheSystem>();

        await server.WaitAssertion(() =>
        {
            var technologies = protoMan.EnumeratePrototypes<TechnologyPrototype>().ToList();

            Assert.Multiple(() =>
            {
                foreach (var discipline in protoMan.EnumeratePrototypes<TechDisciplinePrototype>())
                {
                    foreach (var technology in technologies)
                    {
                        if (technology.Discipline != discipline.ID || technology.Tier == 1)
                            continue;

                        Assert.That(technology.Tier, Is.GreaterThan(0),
                            $"Technology {technology} has invalid tier {technology.Tier}.");
                        Assert.That(discipline.TierPrerequisites.ContainsKey(technology.Tier),
                            $"Discipline {discipline.ID} does not have a TierPrerequisites definition for tier {technology.Tier}");
                    }
                }
            });

            var latheRecipes = new HashSet<ProtoId<LatheRecipePrototype>>();
            foreach (var prototype in protoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract || pair.IsTestPrototype(prototype)
                    || !prototype.TryComp<LatheComponent>(out var lathe, compFact))
                    continue;

                latheSystem.AddRecipesFromPacks(latheRecipes, lathe.DynamicPacks);

                if (prototype.TryComp<EmagLatheRecipesComponent>(out var emag, compFact))
                    latheSystem.AddRecipesFromPacks(latheRecipes, emag.EmagDynamicPacks);
            }

            var unlockedRecipes = new HashSet<ProtoId<LatheRecipePrototype>>();
            Assert.Multiple(() =>
            {
                foreach (var technology in technologies)
                {
                    unlockedRecipes.UnionWith(technology.RecipeUnlocks);
                    foreach (var recipe in technology.RecipeUnlocks)
                    {
                        Assert.That(latheRecipes, Does.Contain(recipe),
                            $"Recipe '{recipe}' from tech '{technology.ID}' cannot be unlocked on any lathes.");
                    }
                }

                foreach (var recipe in latheRecipes)
                {
                    Assert.That(unlockedRecipes, Does.Contain(recipe),
                        $"Recipe '{recipe}' is dynamic on a lathe but cannot be unlocked by research.");
                }
            });
        });
        // Starlight - end
    }
    #endregion

    // Moffstation - Start - Blade Server board test to parallel existing machine board test
    /// <summary>
    /// Ensures that every blade server specified to be a machine board's associated blade server also specifies that
    /// its board is that machine board.
    /// </summary>
    [Test]
    public async Task TestBladeServerBoardHasValidBladeServer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFact = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            foreach (var p in protoMan.EnumeratePrototypes<EntityPrototype>()
                         .Where(p => !p.Abstract)
                         .Where(p => !pair.IsTestPrototype(p))
                         .Where(p => !_ignoredPrototypes.Contains(p.ID)))
            {
                if (!p.TryGetComponent<BladeServerBoardComponent>(out var mbc, compFact))
                    continue;

                Assert.Multiple(() =>
                {
                    Assert.That(
                        p.HasComponent<MachineBoardComponent>(compFact),
                        $"Blade server board {p.ID} does not have a {nameof(MachineBoardComponent)}"
                    );
                    Assert.That(
                        protoMan.TryIndex<EntityPrototype>(mbc.Prototype, out var bsProto),
                        $"Blade server board {p.ID}'s corresponding blade server has an invalid prototype."
                    );
                    Assert.That(
                        bsProto!.HasComponent<BladeServerComponent>(compFact),
                        $"Blade server board {p.ID}'s corresponding blade server {mbc.Prototype} does not have {nameof(BladeServerComponent)}"
                    );
                    Assert.That(
                        bsProto!.TryGetComponent<MachineComponent>(out var mComp, compFact),
                        $"Blade server board {p.ID}'s corresponding blade server {mbc.Prototype} does not have {nameof(MachineComponent)}"
                    );
                    Assert.That(
                        mComp!.Board,
                        Is.EqualTo(p.ID),
                        $"Machine {mbc.Prototype}'s BoardPrototype is not equal to its corresponding machine board, {p.ID}"
                    );
                });
            }
        });

        await pair.CleanReturnAsync();
    }
    // Moffstation - End
}
