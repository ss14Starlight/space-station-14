#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
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

        await server.WaitPost(() => Assert.That(sleepingSystem.TrySleeping(serverEntity), Is.True));
        await pair.RunTicksSync(5);
        await pair.RunTicksSync(120);

        var wakeAction = clientActions.GetActions(clientEntity)
            .Single(action => client.ResolveDependency<IEntityManager>()
                .GetComponent<InstantActionComponent>(action).Event is WakeActionEvent);

        clientActions.TriggerAction(wakeAction);
        await pair.RunTicksSync(5);

        Assert.That(server.ResolveDependency<IEntityManager>().HasComponent<SleepingComponent>(serverEntity), Is.False);
    }
}
