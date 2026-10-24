using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server._Starlight.Objectives.ObjectivePicker;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

public sealed partial class ObjectivePickerTest
{
    [Test]
    public async Task RoundEndFormatsCorrectly()
        => await Server.WaitAssertion(() =>
        {
            // An admin-created or ended rule need not have an ActiveGameRule component.
            var rule = SEntMan.SpawnEntity("Thief", MapCoordinates.Nullspace);
            var agent = SEntMan.GetComponent<AntagSelectionComponent>(rule).AgentName!.Value;
            var mind = Server.System<MindSystem>().CreateMind(null, "Picker participant");
            SEntMan.AddComponent(mind, new ObjectivePickerConfigurationComponent { Rule = rule, Finished = true });
            var objective = SEntMan.SpawnEntity(_plain, MapCoordinates.Nullspace);
            Server.System<MetaDataSystem>().SetEntityName(objective, "Incomplete test goal");
            SEntMan.RemoveComponent<FreeObjectiveComponent>(objective);
            SEntMan.AddComponent<SurviveConditionComponent>(objective);
            mind.Comp.Objectives.Add(objective);

            var end = new RoundEndTextAppendEvent();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, end);
            Assert.That(end.Text, Does.Contain("Picker participant"));
            Assert.That(end.Text, Does.Contain(Loc.GetString(agent)));
            Assert.That(end.Text, Does.Contain("Incomplete test goal"));
            Assert.That(end.Text, Does.Contain("Selected Difficulty: 3. Completed Difficulty: 0 (0%)."));
            Assert.That(end.Text, Does.Not.Contain("NUMBER").And.Not.Contain("$percentage"));

            // Active-rule collection and picker fallback must not duplicate the same mind.
            SEntMan.AddComponent<Content.Shared.GameTicking.Components.ActiveGameRuleComponent>(rule);
            var assignedMinds = SEntMan.GetComponent<AntagSelectionComponent>(rule).AssignedMinds;
            assignedMinds.Add("Thief", new() { (mind, "Picker participant") });
            var activeEnd = new RoundEndTextAppendEvent();
            SEntMan.EventBus.RaiseEvent(EventSource.Local, activeEnd);
            Assert.That(activeEnd.Text.Split("Picker participant"), Has.Length.EqualTo(2));
        });
}
