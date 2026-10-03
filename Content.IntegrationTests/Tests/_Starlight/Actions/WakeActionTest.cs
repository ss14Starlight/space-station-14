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
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

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
        var serverEntMan = server.ResolveDependency<IEntityManager>();
        var clientEntMan = client.ResolveDependency<IEntityManager>();

        // The wake action is spawned on the server when the entity falls asleep and has to reach the client through PVS
        // before it can be triggered, so wait for it instead of assuming a fixed number of ticks is enough.
        async Task TriggerWakeAction()
        {
            NetEntity? netAction = null;
            await server.WaitPost(() =>
            {
                var sleeping = serverEntMan.GetComponent<SleepingComponent>(serverEntity);
                Assert.That(sleeping.WakeAction, Is.Not.Null);
                netAction = serverEntMan.GetNetEntity(sleeping.WakeAction!.Value);
            });

            Entity<ActionComponent>? wakeAction = null;
            for (var i = 0; i < 300 && wakeAction == null; i++)
            {
                await client.WaitPost(() =>
                {
                    if (!clientEntMan.TryGetEntity(netAction, out var uid))
                        return;

                    wakeAction = clientActions.GetActions(clientEntity)
                        .Where(action => action.Owner == uid)
                        .Where(action => clientEntMan.TryGetComponent<InstantActionComponent>(action, out var instant)
                            && instant.Event is WakeActionEvent)
                        .Cast<Entity<ActionComponent>?>()
                        .FirstOrDefault();
                });

                if (wakeAction == null)
                    await pair.RunTicksSync(1);
            }

            Assert.That(wakeAction, Is.Not.Null, "Wake action never reached the client");
            await client.WaitPost(() => clientActions.TriggerAction(wakeAction!.Value));
        }

        await server.WaitPost(() => Assert.That(sleepingSystem.TrySleeping(serverEntity), Is.True));
        await pair.RunTicksSync(5);
        await pair.RunTicksSync(120);

        await TriggerWakeAction();
        await pair.RunSeconds(2);
        await pair.ReallyBeIdle();

        Assert.That(server.ResolveDependency<IEntityManager>().HasComponent<SleepingComponent>(serverEntity), Is.False);

        await server.WaitPost(() => Assert.That(sleepingSystem.TrySleeping(serverEntity), Is.True));
        await pair.RunTicksSync(5);
        await pair.RunTicksSync(120);

        await server.WaitPost(() => wakeTestSystem.RejectNextWake = true);
        await TriggerWakeAction();
        await pair.RunTicksSync(5);

        Assert.That(wakeTestSystem.RejectNextWake, Is.False);
        Assert.That(server.ResolveDependency<IEntityManager>().HasComponent<SleepingComponent>(serverEntity), Is.True);

        await pair.RunTicksSync(60);
        await TriggerWakeAction();
        await pair.RunSeconds(2);
        await pair.ReallyBeIdle();

        Assert.That(server.ResolveDependency<IEntityManager>().HasComponent<SleepingComponent>(serverEntity), Is.False);
        await pair.RunUntilSynced();
    }

    private sealed class WakeActionTestSystem : EntitySystem
    {
        public bool RejectNextWake;

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
