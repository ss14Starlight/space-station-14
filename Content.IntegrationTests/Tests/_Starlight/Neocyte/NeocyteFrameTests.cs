using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Humanoid;
using Content.Server.Station.Systems;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Neocyte;

[TestFixture]
[TestOf(typeof(NeocyteSystem))]
public sealed class NeocyteFrameTests : GameTest
{
    private const string FrameSlot = "outerClothing2";

    /// <summary>
    /// Tests that a Neocyte spawned directly into the world receives a random frame from the configured loadout group.
    /// </summary>
    [Test]
    public async Task NeocyteDirectSpawnReceivesRandomFrame()
    {
        var server = Pair.Server;
        var testMap = await Pair.CreateTestMap();
        EntityUid neocyte = default;

        await server.WaitAssertion(() => neocyte = server.EntMan.Spawn("MobNeoHuman", testMap.MapCoords));

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            AssertFrame(
                server.EntMan,
                server.System<InventorySystem>(),
                neocyte,
                GetConfiguredFrames(server.ProtoMan));
            server.EntMan.DeleteEntity(neocyte);
        });
    }

    /// <summary>
    /// Tests that a Neocyte spawned via the player mob spawning system with no job receives their species frame from their profile's loadout.
    /// </summary>
    [Test]
    public async Task NeocyteSpawnPlayerMobWithoutJobPreservesSpeciesFrame()
    {
        var server = Pair.Server;
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var profile = CreateProfileWithFrame("NeocyteArmorHeavySpace");
            var neocyte = server.System<StationSpawningSystem>()
                .SpawnPlayerMob(testMap.GridCoords, job: null, profile, station: null);

            AssertFrame(
                server.EntMan,
                server.System<InventorySystem>(),
                neocyte,
                ["ClothingOuterArmorNeocyteHeavySpaceUnremovable"]);

            server.EntMan.DeleteEntity(neocyte);
        });
    }

    /// <summary>
    /// Tests that a Neocyte spawned via the antagonist loadout system with a frame override
    /// receives the frame from the antag loadout instead of their species loadout.
    /// </summary>
    [Test]
    public async Task NeocyteAntagFrameOverridesSpeciesFrame()
    {
        var server = Pair.Server;
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var profile = CreateProfileWithFrame("NeocyteArmorHeavyBasic");
            var antagLoadout = CreateFrameLoadout("AntagNukeops", "NeocyteArmorMediumMagic");
            var antagLoadoutPrototype = server.ProtoMan.Index<RoleLoadoutPrototype>("AntagNukeops");
            var neocyte = server.EntMan.Spawn("MobNeoHuman", testMap.MapCoords);

            server.System<NeocyteSystem>()
                .EquipSpeciesLoadoutForAntag(neocyte, profile, null, antagLoadout, null);
            server.System<LoadoutSystem>()
                .Equip(neocyte, [], ["AntagNukeops"], antagLoadout, antagLoadoutPrototype);

            AssertFrame(
                server.EntMan,
                server.System<InventorySystem>(),
                neocyte,
                ["ClothingOuterArmorNeocyteMediumMagicUnremovable"]);

            server.EntMan.DeleteEntity(neocyte);
        });
    }

    /// <summary>
    /// Tests that a Neocyte spawned via the antagonist loadout system without an antag loadout
    /// receives the frame from their species loadout instead of a random frame.
    /// </summary>
    [Test]
    public async Task NeocyteAntagWithoutFrameOverridePreservesSpeciesFrame()
    {
        var server = Pair.Server;
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var profile = CreateProfileWithFrame("NeocyteArmorLightSpeed");
            var neocyte = server.EntMan.Spawn("MobNeoHuman", testMap.MapCoords);

            server.System<NeocyteSystem>()
                .EquipSpeciesLoadoutForAntag(neocyte, profile, null, null, null);
            server.System<LoadoutSystem>().Equip(neocyte, [], null);

            AssertFrame(
                server.EntMan,
                server.System<InventorySystem>(),
                neocyte,
                ["ClothingOuterArmorNeocyteLightSpeedUnremovable"]);

            server.EntMan.DeleteEntity(neocyte);
        });
    }

    private static HumanoidCharacterProfile CreateProfileWithFrame(ProtoId<LoadoutPrototype> frame) => HumanoidCharacterProfile.DefaultWithSpecies("NeoHuman")
            .WithSpeciesLoadout(CreateFrameLoadout("NeocyteLoadout", frame));

    private static RoleLoadout CreateFrameLoadout(
        ProtoId<RoleLoadoutPrototype> role,
        ProtoId<LoadoutPrototype> frame)
    {
        var loadout = new RoleLoadout(role);
        loadout.SelectedLoadouts["NeocyteCybernetics"] =
        [
            new Loadout
            {
                Prototype = frame,
            },
        ];
        return loadout;
    }

    private static HashSet<string> GetConfiguredFrames(IPrototypeManager prototypeManager)
    {
        var frames = new HashSet<string>();
        var group = prototypeManager.Index<LoadoutGroupPrototype>("NeocyteCybernetics");

        foreach (var loadoutId in group.Loadouts)
        {
            var loadout = prototypeManager.Index(loadoutId);
            var frame = GetFrameGear(loadout);

            if (string.IsNullOrEmpty(frame) &&
                prototypeManager.Resolve(loadout.StartingGear, out StartingGearPrototype? startingGear))
            {
                frame = GetFrameGear(startingGear);
            }

            if (!string.IsNullOrEmpty(frame))
                frames.Add(frame);
        }

        return frames;
    }

    private static string GetFrameGear(IEquipmentLoadout equipment) => equipment.Equipment.TryGetValue(FrameSlot, out var gear)
            ? gear
            : string.Empty;

    private static void AssertFrame(
        IEntityManager entityManager,
        InventorySystem inventory,
        EntityUid neocyte,
        IReadOnlyCollection<string> expectedPrototypes)
    {
        Assert.That(inventory.TryGetSlotEntity(neocyte, FrameSlot, out var frame), Is.True);
        Assert.That(frame, Is.Not.Null);

        var metadata = entityManager.GetComponent<MetaDataComponent>(frame.Value);
        Assert.That(expectedPrototypes, Does.Contain(metadata.EntityPrototype.ID));
    }
}
