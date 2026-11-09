#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Damage.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._Starlight.FireDragon;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.NPC;
using Content.Shared.StatusEffectNew;
using Content.Shared.Temperature.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Actions;

[TestFixture]
public sealed class FireDragonRoarTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Fresh = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task RoarButtonAndEmoteRespectFullCooldown(bool useClientAction)
    {
        var map = await Pair.CreateTestMap();
        var actions = Server.System<SharedActionsSystem>();
        var status = Server.System<StatusEffectsSystem>();
        var clientActions = Client.System<Content.Client.Actions.ActionsSystem>();
        EntityUid dragon = default;
        EntityUid prey = default;
        NetEntity netDragon = default;
        Entity<ActionComponent>? roar = null;
        EntProtoId hunt = "StatusEffectPredatorHunt";
        ProtoId<EmotePrototype> roarEmote = "FireDragonRoar";

        await Server.WaitAssertion(() =>
        {
            dragon = SEntMan.SpawnAttachedTo("MobFireDragon", new EntityCoordinates(map.Grid, 0, 0));
            prey = SEntMan.SpawnAttachedTo("MobHuman", new EntityCoordinates(map.Grid, 1, 0));
            // Keep the test focused on player input rather than autonomous NPC decisions or vacuum damage.
            SEntMan.RemoveComponent<HTNComponent>(dragon);
            SEntMan.RemoveComponent<ActiveNPCComponent>(dragon);
            Server.System<GodmodeSystem>().EnableGodmode(dragon);
            // Godmode would also block Predator Hunt. Remove ambient-damage components from the prey instead.
            SEntMan.RemoveComponent<RespiratorComponent>(prey);
            SEntMan.RemoveComponent<BarotraumaComponent>(prey);
            SEntMan.RemoveComponent<TemperatureDamageComponent>(prey);

            var session = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
            Assert.That(Server.ResolveDependency<IPlayerManager>().SetAttachedEntity(session, dragon), Is.True);
            netDragon = SEntMan.GetNetEntity(dragon);
            var state = SEntMan.GetComponent<FireDragonComponent>(dragon);
            Assert.That(actions.GetAction(state.RoarActionEntity), Is.Not.Null);
            roar = actions.GetAction(state.RoarActionEntity)!.Value;
        });

        Entity<ActionComponent>? clientRoar = null;
        for (var tick = 0; tick < 300 && clientRoar == null; tick++)
        {
            await Client.WaitPost(() =>
            {
                if (CEntMan.TryGetEntity(netDragon, out var uid)
                    && CEntMan.TryGetComponent<FireDragonComponent>(uid, out var state))
                    clientRoar = clientActions.GetAction(state.RoarActionEntity);
            });
            if (clientRoar == null)
                await Pair.RunTicksSync(1);
        }

        Assert.That(clientRoar, Is.Not.Null, "The dragon's shared Roar action never reached the controlling client.");

        async Task TriggerRoar()
        {
            if (useClientAction)
            {
                await Client.WaitPost(() => clientActions.TriggerAction(clientRoar!.Value));
            }
            else
            {
                await Server.WaitPost(() =>
                {
                    var emote = new EmoteEvent(SProtoMan.Index(roarEmote));
                    SEntMan.EventBus.RaiseLocalEvent(dragon, ref emote);
                });
            }

            await Pair.RunTicksSync(10);
            await Pair.RunUntilSynced();
        }

        await TriggerRoar();
        TimeSpan cooldownEnd = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(status.HasStatusEffect(prey, hunt), Is.True);
            Assert.That(roar!.Value.Comp.Cooldown, Is.Not.Null);
            var cooldown = roar.Value.Comp.Cooldown!.Value;
            Assert.That(cooldown.End - cooldown.Start, Is.EqualTo(TimeSpan.FromSeconds(40)));
            cooldownEnd = cooldown.End;
        });

        // The shared three-second recovery has elapsed, but Roar must still respect its forty-second cooldown.
        await Pair.RunSeconds(4);
        await Server.WaitPost(() => status.TryRemoveStatusEffect(prey, hunt));
        await Pair.RunUntilSynced();
        await TriggerRoar();
        await Server.WaitAssertion(() =>
        {
            Assert.That(status.HasStatusEffect(prey, hunt), Is.False);
            Assert.That(roar!.Value.Comp.Cooldown!.Value.End, Is.EqualTo(cooldownEnd));

            // Even a caller bypassing action validation must not reapply the effect during cooldown.
            actions.PerformAction(dragon, roar.Value, predicted: false);
            Assert.That(status.HasStatusEffect(prey, hunt), Is.False);
        });

        await Pair.RunSeconds(40);
        await TriggerRoar();
        await Server.WaitAssertion(() => Assert.That(status.HasStatusEffect(prey, hunt), Is.True));
    }
}
