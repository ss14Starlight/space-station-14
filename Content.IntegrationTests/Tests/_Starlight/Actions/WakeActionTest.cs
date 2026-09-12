#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared._Starlight.Sleepiness.Events;
using Robust.Shared.GameObjects;
using Robust.Server.Player;

namespace Content.IntegrationTests.Tests._Starlight.Actions;

[TestFixture]
public sealed class WakeActionTest : GameTest
{
    public override PoolSettings PoolSettings => new PoolSettings { Connected = true, DummyTicker = false };

    [Test]
    public async Task SleepingUserCanInvokeWakeAction()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var serverSession = server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var serverEntity = serverSession.AttachedEntity!.Value;
        var clientEntity = client.Session!.AttachedEntity!.Value;
        var sleepingSystem = server.System<SleepingSystem>();
        var clientActions = client.System<Content.Client.Actions.ActionsSystem>();
        var wakeTestSystem = server.System<WakeActionTestSystem>();

        await server.WaitPost(() => Assert.That(sleepingSystem.TrySleeping(serverEntity), Is.True));
        await pair.RunTicksSync(5);
        await pair.RunTicksSync(120);

        await client.WaitPost(() =>
        {
            var wakeAction = clientActions.GetActions(clientEntity)
                .Single(action => client.ResolveDependency<IEntityManager>()
                    .GetComponent<InstantActionComponent>(action).Event is WakeActionEvent);
            clientActions.TriggerAction(wakeAction);
        });
        await pair.RunTicksSync(5);

        Assert.That(server.ResolveDependency<IEntityManager>().HasComponent<SleepingComponent>(serverEntity), Is.False);

        await server.WaitPost(() => Assert.That(sleepingSystem.TrySleeping(serverEntity), Is.True));
        await pair.RunTicksSync(5);
        await pair.RunTicksSync(120);

        await server.WaitPost(() => wakeTestSystem.RejectNextWake = true);
        await client.WaitPost(() =>
        {
            var wakeAction = clientActions.GetActions(clientEntity)
                .Single(action => client.ResolveDependency<IEntityManager>()
                    .GetComponent<InstantActionComponent>(action).Event is WakeActionEvent);
            clientActions.TriggerAction(wakeAction);
        });
        await pair.RunTicksSync(5);

        Assert.That(server.ResolveDependency<IEntityManager>().HasComponent<SleepingComponent>(serverEntity), Is.True);

        await server.WaitPost(() => wakeTestSystem.Cleanup(serverEntity));
        await pair.RunUntilSynced();
    }

    private sealed class WakeActionTestSystem : EntitySystem
    {
        public bool RejectNextWake;

        public void Cleanup(EntityUid entity) => RemComp<SleepingComponent>(entity);

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SleepinessWakeAttemptEvent>(OnWakeAttempt);
        }

        private void OnWakeAttempt(ref SleepinessWakeAttemptEvent args)
        {
            if (!RejectNextWake)
                return;

            RejectNextWake = false;
            args.Result = false;
        }
    }
}
