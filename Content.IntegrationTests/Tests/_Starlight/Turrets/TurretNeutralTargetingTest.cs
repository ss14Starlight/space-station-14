using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Shared.Mech.Components;
using Content.Shared.Turrets;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Turrets;

[TestOf(typeof(TurretTargetSettingsSystem))]
public sealed class TurretNeutralTargetingTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes =
        "\n" +
        "- type: utilityQuery\n" +
        "  id: TestTurretNeutralTargeting\n" +
        "  query:\n" +
        "  - !type:NearbyHostilesQuery\n" +
        "\n" +
        "- type: entity\n" +
        "  id: TestTurretNeutralTargetingTurret\n" +
        "  components:\n" +
        "  - type: NpcFactionMember\n" +
        "    factions:\n" +
        "    - AllHostile\n" +
        "  - type: TurretTargetSettings\n" +
        "\n" +
        "- type: entity\n" +
        "  id: TestTurretNeutralTargetingMob\n" +
        "  components:\n" +
        "  - type: MobState\n" +
        "\n" +
        "- type: entity\n" +
        "  id: TestTurretNeutralTargetingMech\n" +
        "  components:\n" +
        "  - type: Mech\n" +
        "\n" +
        "- type: entity\n" +
        "  id: TestTurretNeutralTargetingActiveNpc\n" +
        "  components:\n" +
        "  - type: ActiveNPC\n" +
        "  - type: MobState\n";

    [SidedDependency(Side.Server)] private readonly TurretTargetSettingsSystem _targetSettings = null!;
    [SidedDependency(Side.Server)] private readonly NPCUtilitySystem _npcUtility = null!;

    [Test]
    public async Task NeutralTargetsAreOptIn()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var turret = SSpawnAtPosition("TestTurretNeutralTargetingTurret", testMap.GridCoords);
            var neutralMob = SSpawnAtPosition("TestTurretNeutralTargetingMob", testMap.GridCoords);
            var neutralMech = SSpawnAtPosition("TestTurretNeutralTargetingMech", testMap.GridCoords);
            var activeNpc = SSpawnAtPosition("TestTurretNeutralTargetingActiveNpc", testMap.GridCoords);
            var settings = new Entity<TurretTargetSettingsComponent>(turret, SComp<TurretTargetSettingsComponent>(turret));
            var blackboard = new NPCBlackboard();
            blackboard.SetValue(NPCBlackboard.Owner, turret);

            Assert.That(SComp<MechComponent>(neutralMech).Broken, Is.False);
            Assert.That(settings.Comp.TargetNeutralEntities, Is.False);
            Assert.That(_targetSettings.EntityIsTargetForTurret(settings, neutralMob), Is.False);
            Assert.That(_targetSettings.EntityIsTargetForTurret(settings, neutralMech), Is.False);
            Assert.That(_targetSettings.EntityIsTargetForTurret(settings, activeNpc), Is.True);
            var defaultTargets = _npcUtility.GetEntities(blackboard, "TestTurretNeutralTargeting", bestOnly: false).Entities;
            Assert.That(defaultTargets.ContainsKey(neutralMob), Is.False);
            Assert.That(defaultTargets.ContainsKey(neutralMech), Is.False);

            _targetSettings.SetTargetNeutralEntities(settings, true);

            Assert.That(_targetSettings.EntityIsTargetForTurret(settings, neutralMob), Is.True);
            Assert.That(_targetSettings.EntityIsTargetForTurret(settings, neutralMech), Is.True);
            var neutralTargets = _npcUtility.GetEntities(blackboard, "TestTurretNeutralTargeting", bestOnly: false).Entities;
            Assert.That(neutralTargets.ContainsKey(neutralMob), Is.True);
            Assert.That(neutralTargets.ContainsKey(neutralMech), Is.True);
            Assert.That(neutralTargets.ContainsKey(activeNpc), Is.False);
        });
    }
}
