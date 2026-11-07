using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Construction.Interaction;

/// <summary>
///     Check that we can build grilles on top of windows, but not the other way around.
/// </summary>
public sealed class GrilleWindowConstruction : InteractionTest
{
    private const string Grille = "Grille";
    private const string Window = "Window";

    [Test]
    public async Task WindowOnGrille()
    {
        // Construct Grille
        await StartConstruction(Grille);
        await InteractUsing(Rod, 10);
        ClientAssertPrototype(Grille, Target);
        var grille = Target;

        // Construct Window
        await StartConstruction(Window);
        // Starlight - start
        await InteractUsing(Glass, 5);
        // Starlight - end
        ClientAssertPrototype(Window, Target);

        // Deconstruct Window
        await Interact(Screw, Wrench);
        AssertDeleted();

        // Deconstruct Grille
        Target = grille;
        await InteractUsing(Cut);
        AssertDeleted();

        // Starlight - start
        await StartConstruction(Window);
        await InteractUsing(Glass, 5);
        ClientAssertPrototype(Window, Target);
        // Starlight - end
    }

    [Test]
    [TestCase(Grille, Grille)]
    [TestCase(Window, Grille)]
    [TestCase(Window, Window)]
    public async Task ConstructionBlocker(string first, string second)
    {
        // Spawn blocking entity
        await SpawnTarget(first);

        // Further construction attempts fail - blocked by first entity interaction.
        await Client.WaitPost(() =>
        {
            var proto = ProtoMan.Index<ConstructionPrototype>(second);
            Assert.That(CConSys.TrySpawnGhost(proto, CEntMan.GetCoordinates(TargetCoords), Direction.South, out _), Is.False);
        });
    }
}
