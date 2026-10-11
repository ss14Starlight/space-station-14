using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Wires;

namespace Content.IntegrationTests.Tests._Starlight.Light;

public sealed class LightFloorTest : InteractionTest
{
    /// <summary>
    /// The bulb of a light floor can only be taken out by hand once the cover is unscrewed.
    /// </summary>
    [Test]
    public async Task BulbOnlyReachableWhenOpen()
    {
        await SpawnTarget("LightFloor");
        var light = Comp<PoweredLightComponent>();
        Assert.That(Comp<WiresPanelComponent>().Open, Is.False);
        Assert.That(light.LightBulbContainer.ContainedEntity, Is.Not.Null);

        await Interact();
        Assert.That(light.LightBulbContainer.ContainedEntity, Is.Not.Null, "bulb ejected through a closed cover");

        await InteractUsing(Screw);
        Assert.That(Comp<WiresPanelComponent>().Open, Is.True);
        await DeleteHeldEntity();
        await Interact();
        Assert.That(light.LightBulbContainer.ContainedEntity, Is.Null, "bulb not ejected from an open floor");
    }
}
