#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server.Destructible;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server._Starlight.Objectives.Components;
using Content.Server._Starlight.Objectives.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

[TestFixture]
[TestOf(typeof(TeachALessonConditionSystem))]
public sealed class ShutdownAiObjectiveTest : GameTest
{
    private const string BluntDamageType = "Blunt";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ShutdownAiObjectiveCompletesOnceAndRemainsCompleteAfterRepair()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid objective = default;
        EntityUid core = default;
        EntityUid aiBody = default;
        BaseContainer? aiContainer = null;
        Entity<MindComponent> ownerMind = default;
        DamageableSystem damageable = null!;
        DamageTypePrototype blunt = null!;

        await server.WaitAssertion(() =>
        {
            var minds = server.System<MindSystem>();
            var ownerBody = SEntMan.SpawnEntity("MobHuman", map.MapCoords);
            ownerMind = minds.CreateMind(null);
            minds.TransferTo(ownerMind, ownerBody);

            aiBody = SEntMan.SpawnEntity("MobHuman", map.MapCoords);
            var aiMind = minds.CreateMind(null);
            minds.TransferTo(aiMind, aiBody);

            ProtoId<TagPrototype> stationAiTag = "StationAi";
            server.System<TagSystem>().AddTag(aiBody, stationAiTag);
            core = SEntMan.SpawnEntity("PlayerStationAiEmpty", map.MapCoords);
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(core, StationAiHolderComponent.Container, out aiContainer),
                Is.True);
            Assert.That(aiContainer, Is.Not.Null);
            Assert.That(containers.Insert(aiBody, aiContainer!), Is.True);

            objective = SEntMan.SpawnEntity("KillStationAiObjective", map.MapCoords);
            Assert.That(SEntMan.HasComponent<TeachALessonConditionComponent>(objective), Is.True);
            Assert.That(SEntMan.HasComponent<KillPersonConditionComponent>(objective), Is.False);

            server.System<TargetObjectiveSystem>().SetTarget(objective, aiMind);
            var afterAssign = new ObjectiveAfterAssignEvent(
                ownerMind.Owner,
                ownerMind.Comp,
                SEntMan.GetComponent<ObjectiveComponent>(objective),
                SEntMan.GetComponent<MetaDataComponent>(objective));
            SEntMan.EventBus.RaiseLocalEvent(objective, ref afterAssign);

            damageable = server.System<DamageableSystem>();
            blunt = server.ResolveDependency<IPrototypeManager>().Index<DamageTypePrototype>(BluntDamageType);
        });

        await server.WaitAssertion(() =>
        {
            var objectives = server.System<SharedObjectivesSystem>();
            Assert.That(objectives.GetProgress(objective, ownerMind), Is.Zero);

            damageable.ChangeDamage(core, new DamageSpecifier(blunt, 400), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            Assert.That(objectives.GetProgress(objective, ownerMind), Is.EqualTo(1f));

            damageable.ChangeDamage(core, new DamageSpecifier(blunt, -400), ignoreResistances: true,
                ignoreGlobalModifiers: true);
            Assert.That(SEntMan.GetComponent<DestructibleComponent>(core).IsBroken, Is.False);
            Assert.That(objectives.GetProgress(objective, ownerMind), Is.EqualTo(1f));
        });

        await server.WaitAssertion(() =>
        {
            var containers = server.System<SharedContainerSystem>();
            if (aiContainer?.Contains(aiBody) == true)
                Assert.That(containers.Remove(aiBody, aiContainer), Is.True);
        });
    }
}
