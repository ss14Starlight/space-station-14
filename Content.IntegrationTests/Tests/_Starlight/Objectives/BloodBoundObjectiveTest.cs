using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Buckle.Systems;
using Content.Server.Mind;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Server._Starlight.Objectives.Components;
using Content.Server._Starlight.Objectives.Systems;
using Content.Server._Starlight.Objectives.Targeting;
using Content.Server._Starlight.Zones;
using Content.Shared._Starlight.Antags.BloodBound.Components;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared._Starlight.Traits.Antags;
using Content.Shared._Starlight.Zones;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

public sealed class BloodBoundObjectiveTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

[Test]
public async Task StealHeartObjectiveTest()
    => await Server.WaitAssertion(() =>
    {
        var minds = Server.System<MindSystem>();
        var hearts = Server.System<StealHeartConditionSystem>();
        var bodies = Server.System<SharedBodySystem>();
        var containers = Server.System<SharedContainerSystem>();
        var objectives = Server.System<SharedObjectivesSystem>();
        var tags = Server.System<TagSystem>();

        var owner = SEntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        var ownerMind = minds.CreateMind(null);
        minds.TransferTo(ownerMind, owner);

        var target = SEntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        var targetMind = minds.CreateMind(null);
        minds.TransferTo(targetMind, target);

        var ipc = SEntMan.SpawnEntity("MobIPC", MapCoordinates.Nullspace);
        var ipcMind = minds.CreateMind(null);
        minds.TransferTo(ipcMind, ipc);

        var filter = new HasHeartMindFilter();
        ProtoId<TagPrototype> heartTag = "Heart";
        var heart = hearts.GetHearts(target).Single();

        tags.RemoveTag(heart, heartTag);
        Assert.That(filter.Filter(targetMind, ownerMind, (EntityManager) SEntMan), Is.True,
            "A heart without the Heart tag must not qualify.");

        tags.AddTag(heart, heartTag);
        Assert.That(filter.Filter(targetMind, ownerMind, (EntityManager) SEntMan), Is.False);
        Assert.That(filter.Filter(ipcMind, ownerMind, (EntityManager) SEntMan), Is.True,
            "IPC pumps must not qualify.");

        var weighted = new WeightedAlivePool();
        Assert.That(weighted.Weight(targetMind, SEntMan), Is.EqualTo(1));
        SEntMan.AddComponent<MarkedForDeathComponent>(target);
        Assert.That(weighted.Weight(targetMind, SEntMan), Is.EqualTo(2));

        var objective = SEntMan.SpawnEntity("BloodBoundStealHeartObjective", MapCoordinates.Nullspace);
        Server.System<TargetObjectiveSystem>().SetTarget(objective, targetMind);
        var assigned = new ObjectiveAssignedEvent(ownerMind, ownerMind.Comp);
        SEntMan.EventBus.RaiseLocalEvent(objective, ref assigned);
        Assert.That(assigned.Cancelled, Is.False);

        var inventory = containers.EnsureContainer<Container>(owner, "blood-bound-test-inventory");

        Assert.That(containers.Insert(target, inventory), Is.True);
        Assert.That(objectives.GetProgress(objective, ownerMind), Is.Zero,
            "Carrying the whole victim must not count.");
        containers.Remove(target, inventory);

        var wrongHeart = SEntMan.SpawnEntity("OrganHumanHeart", MapCoordinates.Nullspace);
        containers.Insert(wrongHeart, inventory);
        Assert.That(objectives.GetProgress(objective, ownerMind), Is.Zero,
            "An unrelated heart must not count.");

        Assert.That(bodies.RemoveOrgan(heart), Is.True);
        Assert.That(filter.Filter(targetMind, ownerMind, (EntityManager) SEntMan), Is.True,
            "A person with no heart must not be selected.");

        containers.Insert(heart, inventory);
        Assert.That(objectives.GetProgress(objective, ownerMind), Is.EqualTo(1));
        containers.Remove(heart, inventory);
        Assert.That(objectives.GetProgress(objective, ownerMind), Is.Zero);

        // Follow the same target mind into a replacement body.
        var clone = SEntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        minds.TransferTo(targetMind, clone);
        var cloneHeart = hearts.GetHearts(clone).Single();
        Assert.That(bodies.RemoveOrgan(cloneHeart), Is.True);
        containers.Insert(cloneHeart, inventory);
        Assert.That(objectives.GetProgress(objective, ownerMind), Is.EqualTo(1));
    });

    [Test]
    public async Task FirestakeObjectiveTest()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid objective = default;
        EntityUid preview = default;
        EntityUid stake = default;
        EntityUid gridUid = default;
        Entity<MindComponent> ownerMind = default;
        Entity<MindComponent> victimMind = default;
        EntityCoordinates targetCoords = default;

        await Server.WaitAssertion(() =>
        {
            var air = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            air.SetMoles(Gas.Oxygen, 21.824779f);
            air.SetMoles(Gas.Nitrogen, 82.10312f);
            Server.System<AtmosphereSystem>().SetMapAtmosphere(testMap.MapUid, false, air);

            var maps = Server.System<SharedMapSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var grid = maps.CreateGridEntity(testMap.MapId);
            gridUid = grid;

            for (var x = 0; x < 10; x++)
            for (var y = 0; y < 4; y++)
                maps.SetTile(grid, grid, new Vector2i(x, y), new Tile(tiles["Plating"].TileId));

            for (var y = 0; y < 4; y++)
            {
                var boundary = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, 4.5f, y + 0.5f));
                SEntMan.AddComponent<ZoneBoundaryComponent>(boundary);
                transform.AnchorEntity(boundary);
            }

            var zoneGrid = SEntMan.AddComponent<ZoneGridComponent>(grid);
            zoneGrid.Shapes =
            [
                new ZoneShapeSet { Zone = "Cargo", Rects = [new Box2i(0, 0, 5, 4)] },
                new ZoneShapeSet { Zone = "Medical", Rects = [new Box2i(5, 0, 10, 4)] },
            ];

            var zones = Server.System<ZoneSystem>();
            zones.RebuildHints((grid, zoneGrid));
            zones.FullRebuild((grid, zoneGrid));

            ProtoId<ZonePrototype>[] expectedZones = ["Cargo", "Medical"];
            Assert.That(zones.GetGridZones(grid), Is.EquivalentTo(expectedZones));

            var minds = Server.System<MindSystem>();
            var owner = SEntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, 1.5f, 1.5f));
            ownerMind = minds.CreateMind(null);
            minds.TransferTo(ownerMind, owner);

            objective = SEntMan.SpawnEntity("BloodBoundFirestakeObjective", MapCoordinates.Nullspace);
            var assigned = new ObjectiveAssignedEvent(ownerMind, ownerMind.Comp);
            SEntMan.EventBus.RaiseLocalEvent(objective, ref assigned);
            Assert.That(assigned.Cancelled, Is.False);

            var numbers = Server.System<NumberObjectiveSystem>();
            Assert.That(numbers.GetTarget(objective), Is.InRange(1, 3));
            numbers.SetTarget(objective, 3);
            minds.AddObjective(ownerMind, ownerMind.Comp, objective);

            var selected = SEntMan.GetComponent<FirestakeConditionComponent>(objective);
            var isCargo = selected.Zone!.Value.Id == "Cargo";
            targetCoords = new EntityCoordinates(grid, isCargo ? 1.5f : 7.5f, 1.5f);
            var wrongCoords = new EntityCoordinates(grid, isCargo ? 7.5f : 1.5f, 1.5f);

            preview = SEntMan.SpawnEntity("BloodBoundFirestakeObjective", MapCoordinates.Nullspace);
            var previewAssigned = new ObjectiveAssignedEvent(ownerMind, ownerMind.Comp);
            SEntMan.EventBus.RaiseLocalEvent(preview, ref previewAssigned);
            Assert.That(previewAssigned.Cancelled, Is.False);
            SEntMan.GetComponent<FirestakeConditionComponent>(preview).Zone = selected.Zone;

            (stake, victimMind) = BurnPerson(wrongCoords);
        });

        await RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FirestakeConditionComponent>(objective).Victims, Is.Empty,
                "Burning someone in a different zone must not count.");

            Server.System<SharedTransformSystem>().SetCoordinates(stake, targetCoords);
        });

        await RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FirestakeConditionComponent>(objective).Victims,
                Has.Count.EqualTo(1), "Burning someone in the selected zone must count once.");
            Assert.That(SEntMan.GetComponent<FirestakeConditionComponent>(preview).Victims, Is.Empty,
                "An unselected offer must not accumulate progress.");
            Assert.That(Server.System<SharedObjectivesSystem>().GetProgress(objective, ownerMind),
                Is.EqualTo(1f / 3).Within(0.001));

            BurnPerson(new EntityCoordinates(gridUid, targetCoords.X, 2.5f), victimMind);
            BurnPerson(new EntityCoordinates(gridUid, targetCoords.X + 1, 2.5f), dead: true);

            BurnPerson(new EntityCoordinates(gridUid, targetCoords.X, 0.5f), ownerMind);
            var (_, bloodBoundMind) = BurnPerson(new EntityCoordinates(gridUid, targetCoords.X + 1, 0.5f));
            Server.System<RoleSystem>().MindAddRole(bloodBoundMind, "MindRoleBloodBound");
            var (_, bloodBoundBodyMind) = BurnPerson(new EntityCoordinates(gridUid, targetCoords.X + 2, 0.5f));
            SEntMan.AddComponent<BloodBoundComponent>(bloodBoundBodyMind.Comp.OwnedEntity!.Value);
        });

        await RunTicksSync(40);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<FirestakeConditionComponent>(objective).Victims,
                Has.Count.EqualTo(1), "The owner, other Blood Bound, replacement bodies, and already dead victims must not add progress."));

        (EntityUid Stake, Entity<MindComponent> Mind) BurnPerson(
            EntityCoordinates coords,
            Entity<MindComponent>? existingMind = null,
            bool dead = false)
        {
            var minds = Server.System<MindSystem>();
            var body = SEntMan.SpawnEntity("MobHuman", coords);
            var mind = existingMind ?? minds.CreateMind(null);
            minds.TransferTo(mind, body);

            if (dead)
            {
                Server.System<DamageableSystem>().TryChangeDamage(body, new DamageSpecifier
                {
                    DamageDict = new() { ["Heat"] = 300 },
                });
                Assert.That(Server.System<MobStateSystem>().IsDead(body), Is.True);
            }

            var bonfire = SEntMan.SpawnEntity("BonfireStake", coords);
            Assert.That(Server.System<BuckleSystem>().TryBuckle(body, null, bonfire), Is.True);
            Server.System<FlammableSystem>().SetFireStacks(body, 1, ignite: true);
            return (bonfire, mind);
        }
    }
}
