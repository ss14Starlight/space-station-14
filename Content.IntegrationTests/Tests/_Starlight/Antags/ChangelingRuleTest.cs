using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server.Roles;
using Content.Server._Starlight.Objectives.ObjectivePicker;
using Content.Server._Starlight.Roles;
using Content.Shared.Antag;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared._Starlight.Traits.Antags;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Starlight.Antags;

public sealed class ChangelingRuleTest : GameTest
{
    private static readonly EntProtoId _human = "MobHuman";
    private static readonly EntProtoId _rule = "SLChangeling";
    private static readonly EntProtoId _role = "MindRoleChangeling";
    private static readonly ProtoId<AntagSpecifierPrototype> _specifier = "SLChangeling";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ChangelingHasFourOptionalCombatOffersAndItsOwnBriefing()
    {
        EntityUid mindId = default;
        await Server.WaitAssertion(() =>
        {
            // Check the player and target are spawned.
            var body = SEntMan.SpawnEntity(_human, MapCoordinates.Nullspace);
            var mind = Server.System<MindSystem>().CreateMind(ServerSession!.UserId);
            mindId = mind;
            Server.System<MindSystem>().TransferTo(mind, body);
            var target = SEntMan.SpawnEntity(_human, MapCoordinates.Nullspace);
            var targetMind = Server.System<MindSystem>().CreateMind(null);
            Server.System<MindSystem>().TransferTo(targetMind, target);
            SEntMan.AddComponent<MarkedForDeathComponent>(target);

            // Give the player a crew briefing before making them a changeling.
            var roles = Server.System<RoleSystem>();
            roles.MindAddJobRole(mind, jobPrototype: "Assistant", silent: true);
            Assert.That(roles.MindHasRole<JobRoleComponent>(mind, out var crew), Is.True);
            SEntMan.EnsureComponent<RoleBriefingComponent>(crew.Value.Owner).Briefing = "Generic crew briefing";
            roles.MindAddRole(mind, _role, silent: true);

            // Add the game rule and make the player a changeling.
            var rule = SEntMan.SpawnEntity(_rule, MapCoordinates.Nullspace);
            var ev = new AfterAntagEntitySelectedEvent(ServerSession, body,
                (rule, SEntMan.GetComponent<AntagSelectionComponent>(rule)), SProtoMan.Index(_specifier));
            SEntMan.EventBus.RaiseLocalEvent(rule, ref ev);

            // Fixed goals are assigned; additional random goals wait for confirmation.
            var offers = SEntMan.GetComponent<PotentialObjectivesComponent>(mind);
            Assert.That(offers.MinimumDifficulty, Is.Zero);
            Assert.That(offers.ObjectiveOptions, Has.Count.EqualTo(4));
            Assert.That(offers.ObjectiveOptions.Values.Count(info => info.Title.Contains("high value")), Is.EqualTo(2));
            foreach (var id in offers.ObjectiveOptions.Keys)
            {
                Assert.That(SEntMan.GetComponent<TargetObjectiveComponent>(SEntMan.GetEntity(id)).Target, Is.Null);
                if (offers.ObjectiveOptions[id].Title.Contains("high value"))
                    Assert.That(SEntMan.GetComponent<PickRandomPersonComponent>(SEntMan.GetEntity(id)).Pool,
                        Is.TypeOf<HighValueTargetsPool>());
            }
            Assert.That(mind.Comp.Objectives.All(uid => SEntMan.GetComponent<ObjectiveComponent>(uid).Difficulty == 0), Is.True);
            // Check the changeling greeting is stored on its role and the crew briefing is preserved.
            Assert.That(roles.MindHasRole<SLChangelingRoleComponent>(mind, out var changelingRole), Is.True);
            Assert.That(SEntMan.GetComponent<RoleBriefingComponent>(changelingRole.Value.Owner).Briefing,
                Is.EqualTo(Loc.GetString("changeling-role-greeting",
                    ("name", SEntMan.GetComponent<MetaDataComponent>(body).EntityName))));
            Assert.That(SEntMan.GetComponent<RoleBriefingComponent>(crew.Value.Owner).Briefing,
                Is.EqualTo("Generic crew briefing"));

            // The player can decline all optional combat goals.
            Server.System<AntagRandomObjectivesSystem>().ApplySelectedObjectives(mind, []);
            Assert.That(SEntMan.GetComponent<ObjectivePickerConfigurationComponent>(mind).Finished, Is.True);
        });
        await Pair.RunTicksSync(5);
        // Check that confirming an empty selection closes the picker without unlocking more goals.
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<PotentialObjectivesComponent>(mindId), Is.False);
            Assert.That(SEntMan.GetComponent<ObjectivePickerProgressComponent>(mindId).CanPickMore, Is.False);
        });
    }
}
