using Content.IntegrationTests.Fixtures;
using Content.Shared._Starlight.EnergyColor;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Starlight;

/// <summary>
/// Verifies that VV color edits reach the client and survive item activation.
/// </summary>
public sealed class EnergyColorTest : GameTest
{
    [Test]
    public async Task VvWriteColorWhileOffPersistsThroughActivation()
    {
        var map = await Pair.CreateTestMap();
        var sword = await SpawnAtPosition("EnergySword", map.GridCoords);
        var console = Server.ResolveDependency<IConsoleHost>();
        var toggle = Server.System<ItemToggleSystem>();
        var green = Color.FromHex("#00FF00");
        var magenta = Color.FromHex("#FF00FF");

        // Exercise the console's quoting and YAML deserialization on a sword that spawned off.
        // The console removes the double quotes; the single quotes keep '#' from starting a YAML comment.
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<ItemToggleComponent>(sword).Activated, Is.False);
            console.ExecuteCommand($"vvwrite /entity/{SEntMan.GetNetEntity(sword)}/EnergyColor/ActiveColor \"'#00FF00'\"");
            AssertServerColor(sword, green);
            Assert.That(SComp<ItemToggleComponent>(sword).Activated, Is.False);
        });
        await AssertClientColor(sword, green, false);

        await Server.WaitAssertion(() =>
        {
            Assert.That(toggle.TrySetActive(sword, true), Is.True);
            AssertServerColor(sword, green);
        });
        await AssertClientColor(sword, green, true);

        // A second write must update the already replicated component and visible blade.
        await Server.WaitAssertion(() =>
        {
            console.ExecuteCommand($"vvwrite /entity/{SEntMan.GetNetEntity(sword)}/EnergyColor/ActiveColor \"'#FF00FF'\"");
            AssertServerColor(sword, magenta);
        });
        await AssertClientColor(sword, magenta, true);

        await Server.WaitAssertion(() => Assert.That(toggle.TrySetActive(sword, false), Is.True));
        await AssertClientColor(sword, magenta, false);
        await Server.WaitAssertion(() => Assert.That(toggle.TrySetActive(sword, true), Is.True));
        await AssertClientColor(sword, magenta, true);

        await Server.WaitPost(() => SDeleteNow(map.MapUid));
    }

    private void AssertServerColor(EntityUid sword, Color expected)
    {
        Assert.That(SComp<EnergyColorComponent>(sword).ActiveColor, Is.EqualTo(expected));
        Assert.That(Server.System<SharedAppearanceSystem>()
            .TryGetData<Color>(sword, ToggleableVisuals.Color, out var appearanceColor), Is.True);
        Assert.That(appearanceColor, Is.EqualTo(expected));
    }

    private async Task AssertClientColor(EntityUid sword, Color expected, bool active)
    {
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientSword = ToClientUid(sword);
            Assert.That(CComp<EnergyColorComponent>(clientSword).ActiveColor, Is.EqualTo(expected));
            Assert.That(CComp<ItemToggleComponent>(clientSword).Activated, Is.EqualTo(active));
            Assert.That(Client.System<SharedAppearanceSystem>()
                .TryGetData<Color>(clientSword, ToggleableVisuals.Color, out var appearanceColor), Is.True);
            Assert.That(appearanceColor, Is.EqualTo(expected));

            var sprites = Client.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet(clientSword, "blade", out var layerIndex, false), Is.True);
            Assert.That(sprites.TryGetLayer(clientSword, layerIndex, out var blade, false), Is.True);
            Assert.That(blade.Color, Is.EqualTo(expected));
            Assert.That(blade.Visible, Is.EqualTo(active));
        });
    }
}
