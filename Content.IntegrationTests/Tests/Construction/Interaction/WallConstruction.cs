using Content.IntegrationTests.Tests.Interaction;

namespace Content.IntegrationTests.Tests.Construction.Interaction;

public sealed class WallConstruction : InteractionTest
{
    public const string Girder = "Girder";
    public const string WallSolid = "WallSolid";
    public const string Wall = "Wall";

    [Test]
    public async Task ConstructAndDeconstructWall() // Starlight
    {
        await StartConstruction(Wall);
        await InteractUsing(Steel, 2);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        ClientAssertPrototype(Girder, Target);
        await InteractUsing(Steel, 2);
        Assert.That(HandSys.GetActiveItem((SEntMan.GetEntity(Player), Hands)), Is.Null);
        AssertPrototype(WallSolid);

        // Starlight - start
        await InteractUsing(Weld);
        AssertPrototype(Girder);
        await Interact(Wrench, Screw);
        AssertDeleted();
        await AssertEntityLookup((Steel, 4));
        // Starlight - end
    }
}
