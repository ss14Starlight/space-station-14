using System.Collections.Generic;
using System.Linq;
using Content.Client._Moffstation.CharacterMenu;
using Content.Server.Mind;
using Content.Shared._Moffstation.Objectives;
using Content.Shared._Starlight.Character.Info.Components;
using Content.Shared._Starlight.Railroading.Components;
using Content.Shared.Mind;
using Content.Shared.Tag;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.IntegrationTests.Tests.GameRules;

public sealed partial class TraitorRuleTest
{
    private static readonly ProtoId<TagPrototype> _sLBackgroundTag = "CivilianTraitBackground";

    private async Task SLTestObjectivePicker(EntityUid mind, EntityUid player)
    {
        Dictionary<NetEntity, EntityUid> candidates = null;
        EntityUid foreignMind = default;
        EntityUid foreignObjective = default;
        NetEntity foreignMindNet = default;
        NetEntity foreignObjectiveNet = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).Objectives, Is.Empty,
                "Generating offers must not assign them to the mind.");
            candidates = SEntMan.GetComponent<PotentialObjectivesComponent>(mind).ObjectiveOptions.Keys
                .ToDictionary(objective => objective, SEntMan.GetEntity);

            // Starlight always contributes a Cards group, even without an active card.
            SEntMan.EnsureComponent<RailroadableComponent>(player);
#pragma warning disable RA0002 // Allow direct component initialization in test setup.
            SEntMan.AddComponent(player, new CharacterDescriptionComponent { Description = "Physical description" }, true);
            SEntMan.AddComponent(mind, new CharacterDescriptionComponent { Description = "Personality description" }, true);
            SEntMan.AddComponent(mind, new RoleplayInfoComponent { OOCNotes = "OOC notes" }, true);
            SEntMan.AddComponent(mind, new MindSecretsComponent { PersonalNotes = "Personal notes" }, true);
#pragma warning restore RA0002
            Server.System<TagSystem>().AddTag(player, _sLBackgroundTag);

            foreignMind = Server.System<MindSystem>().CreateMind(null);
            foreignObjective = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            foreignMindNet = SEntMan.GetNetEntity(foreignMind);
            foreignObjectiveNet = SEntMan.GetNetEntity(foreignObjective);
            SEntMan.AddComponent(foreignMind, new PotentialObjectivesComponent
            {
                ObjectiveOptions = new() { [SEntMan.GetNetEntity(foreignObjective)] = default },
            });
        });
        await Pair.RunUntilSynced();

        var selected = candidates.Keys.Take(1).ToHashSet();
        var ui = Client.ResolveDependency<IUserInterfaceManager>();
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.System<SharedMindSystem>().TryGetMind(Client.Session, out var clientMind, out _));
            Assert.That(CEntMan.GetComponent<PotentialObjectivesComponent>(clientMind).ObjectiveOptions.Keys,
                Is.EquivalentTo(candidates.Keys));
            ui.GetUIController<CharacterUIController>().OpenCharacterOverview();
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var window = SLFindControls<MoffCharacterWindow>(ui.RootControl).Single();

            // Resolve the named XAML controls through the public lookup API.
            var characterInfoTabs = window.FindControl<TabContainer>("CharacterInfoTabs");
            var characterDescription = window.FindControl<Control>("InfoIC").FindControl<RichTextLabel>("CharacterDesc");
            var oocInfo = window.FindControl<Control>("InfoOOC");
            var oocNotes = oocInfo.FindControl<RichTextLabel>("OOCNotes");
            var personalNotes = oocInfo.FindControl<RichTextLabel>("PersonalNotes");
            var background = window.FindControl<Control>("InfoBackground").FindControl<RichTextLabel>("Background");
            var objectivesWrapper = window.FindControl<BoxContainer>("ObjectivesWrapper");

            Assert.That(characterInfoTabs.Children.Count, Is.EqualTo(2));
            Assert.That(characterInfoTabs.CurrentTab, Is.Zero);
            Assert.That(characterDescription.GetMessage(), Does.Contain("Physical description"));
            Assert.That(characterDescription.GetMessage(), Does.Contain("Personality description"));
            Assert.That(oocNotes.GetMessage(), Does.Contain("OOC notes"));
            Assert.That(personalNotes.GetMessage(), Does.Contain("Personal notes"));
            Assert.That(background.GetMessage(), Does.Contain(Loc.GetString("trait-background-civilian-name")));
            Assert.That(SLFindControls<Button>(objectivesWrapper).Select(button => button.Text),
                Does.Contain(Loc.GetString("objective-picker-button")),
                "The empty Cards group must not hide the objective picker.");

            characterInfoTabs.CurrentTab = 1;
            window.Close();
            Assert.That(characterDescription.GetMessage(), Is.Null);
            Assert.That(personalNotes.GetMessage(), Is.Null);
            ui.GetUIController<CharacterUIController>().OpenCharacterOverview();
            Assert.That(characterInfoTabs.CurrentTab, Is.Zero);
            Assert.That(oocNotes.GetMessage(), Does.Contain("OOC notes"));

            // A client must not be able to submit for another mind.
            Client.ResolveDependency<IEntityNetworkManager>().SendSystemNetworkMessage(new ObjectivePickerSelected
            {
                MindId = foreignMindNet,
                SelectedObjectives = new() { foreignObjectiveNet },
            });
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<MindComponent>(foreignMind).Objectives, Is.Empty);
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).Objectives, Is.Empty);
        });

        await Client.WaitAssertion(() => Client.ResolveDependency<IEntityNetworkManager>().SendSystemNetworkMessage(new ObjectivePickerSelected
        {
            MindId = CEntMan.GetNetEntity(Client.System<SharedMindSystem>().GetMind(Client.User!.Value)!.Value),
            SelectedObjectives = selected,
        }));
        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).Objectives,
                Is.EquivalentTo(selected.Select(objective => candidates[objective])));
            Assert.That(SEntMan.HasComponent<PotentialObjectivesComponent>(mind), Is.False);
            foreach (var unused in candidates.Where(candidate => !selected.Contains(candidate.Key)))
            {
                Assert.That(SEntMan.EntityExists(unused.Value), Is.False, "Unused offers must be deleted.");
            }
        });
    }

    [Test]
    public async Task TestObjectiveTimeoutDoesNotSkipOtherMinds()
    {
        EntityUid futureMind = default;
        EntityUid expiredMind = default;
        EntityUid expiredObjective = default;
        await Server.WaitAssertion(() =>
        {
            var minds = Server.System<MindSystem>();
            futureMind = minds.CreateMind(null);
            expiredMind = minds.CreateMind(null);
            var futureObjective = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            expiredObjective = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);

            SEntMan.AddComponent(futureMind, new PotentialObjectivesComponent
            {
                AutoSelectionDelay = TimeSpan.FromHours(1),
                MaxChoices = 1,
                ObjectiveOptions = new() { [SEntMan.GetNetEntity(futureObjective)] = default },
            });
            SEntMan.AddComponent(expiredMind, new PotentialObjectivesComponent
            {
                AutoSelectionDelay = TimeSpan.Zero,
                MaxChoices = 1,
                ObjectiveOptions = new() { [SEntMan.GetNetEntity(expiredObjective)] = default },
            });
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<MindComponent>(futureMind).Objectives, Is.Empty);
            Assert.That(SEntMan.HasComponent<PotentialObjectivesComponent>(futureMind), Is.True);
            Assert.That(SEntMan.GetComponent<MindComponent>(expiredMind).Objectives,
                Is.EqualTo(new[] { expiredObjective }));
            Assert.That(SEntMan.HasComponent<PotentialObjectivesComponent>(expiredMind), Is.False);
        });
    }

    private static IEnumerable<T> SLFindControls<T>(Control control) where T : Control
    {
        if (control is T found)
            yield return found;

        foreach (var child in control.Children)
        {
            foreach (var descendant in SLFindControls<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
