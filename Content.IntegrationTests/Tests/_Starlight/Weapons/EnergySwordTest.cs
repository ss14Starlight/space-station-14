using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee.EnergySword;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.ViewVariables;

namespace Content.IntegrationTests.Tests._Starlight.Weapons;

/// <summary>
/// Guards character-script blade colour changes through VV: writes work while off or on and survive toggling. This was made for staff to use for admemes and events mainly to colour eswords easier.
/// </summary>
[TestFixture]
public sealed class EnergySwordTest
{
    [Test]
    public async Task VvWriteBladeColor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var vvm = server.ResolveDependency<IViewVariablesManager>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("EnergySword", MapCoordinates.Nullspace);
            var sword = entMan.GetComponent<EnergySwordComponent>(uid);
            var toggle = entMan.GetComponent<ItemToggleComponent>(uid);
            var toggles = entMan.System<ItemToggleSystem>();
            var appearance = entMan.System<SharedAppearanceSystem>();
            var path = $"/entity/{uid}/EnergySword/ActivatedColor";

            void AssertColor(Color expected, bool active)
            {
                Assert.That(sword.ActivatedColor, Is.EqualTo(expected));
                Assert.That(appearance.TryGetData<Color>(uid, ToggleableVisuals.Color, out var actual), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(toggle.Activated, Is.EqualTo(active));
            }

            // Hex values must remain quoted for VV's YAML deserializer.
            var firstColor = Color.FromHex("#12ABEF");
            Assert.That(toggle.Activated, Is.False);
            Assert.That(vvm.ResolvePath(path), Is.Not.Null);
            vvm.WritePath(path, "\"#12ABEF\"");
            AssertColor(firstColor, false);

            Assert.That(toggles.TryActivate(uid), Is.True);
            AssertColor(firstColor, true);

            var secondColor = Color.FromHex("#EF45AB");
            vvm.WritePath(path, "\"#EF45AB\"");
            AssertColor(secondColor, true);

            Assert.That(toggles.TryDeactivate(uid), Is.True);
            AssertColor(secondColor, false);
            Assert.That(toggles.TryActivate(uid), Is.True);
            AssertColor(secondColor, true);

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }
}
