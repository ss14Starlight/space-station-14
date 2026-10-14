using System.Collections.Generic;
using System.Linq;
using Content.Client._Starlight.Character.Info.UI;
using Content.Client._Starlight.Objectives.ObjectivePicker;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server._Starlight.Objectives.Components;
using Content.Server._Starlight.Objectives.ObjectivePicker;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared._Starlight.Character.Info;
using Content.Shared._Starlight.CCVar;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Objectives.Components;
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

    private async Task TestObjectivePicker(EntityUid mind, EntityUid player)
    {
        await OverrideCVar(Side.Server, CCVars.FlavorText, true);
        await OverrideCVar(Side.Server, StarlightCCVars.OOCNotes, true);

        Dictionary<NetEntity, EntityUid> candidates = null;
        HashSet<NetEntity> selected = null;
        EntityUid forced = default;
        EntityUid foreignMind = default;
        EntityUid foreignObjective = default;
        NetEntity foreignMindNet = default;
        NetEntity foreignObjectiveNet = default;
        await Server.WaitAssertion(() =>
        {
            var mindComp = SEntMan.GetComponent<MindComponent>(mind);
            Assert.That(mindComp.Objectives.Count, Is.EqualTo(1), "Only the forced objective is assigned before confirmation.");
            forced = mindComp.Objectives.Single();
            var offers = SEntMan.GetComponent<PotentialObjectivesComponent>(mind);
            Assert.That(ObjectivePickerSelection.Difficulty(offers, offers.ObjectiveOptions.Keys),
                Is.GreaterThanOrEqualTo(2 * offers.MinimumDifficulty));
            var storyCount = offers.Difficulties.Count(pair => pair.Value == 0);
            Assert.That(storyCount, Is.EqualTo(SEntMan.HasComponent<DieConditionComponent>(forced) ? 0 : 2));
            foreach (var objective in offers.ObjectiveOptions.Keys.Select(SEntMan.GetEntity))
            {
                if ((SEntMan.HasComponent<KillPersonConditionComponent>(objective) ||
                        SEntMan.HasComponent<TeachALessonConditionComponent>(objective)) &&
                            SEntMan.TryGetComponent<TargetObjectiveComponent>(objective, out var target))
                    Assert.That(target.Target, Is.Null, "Unconfirmed kill/lesson offers must not have targets.");
            }
            candidates = SEntMan.GetComponent<PotentialObjectivesComponent>(mind).ObjectiveOptions.Keys
                .ToDictionary(objective => objective, SEntMan.GetEntity);

            SEntMan.EnsureComponent<RailroadableComponent>(player);

            // Replace existing notes before applying a profile through the owning system.
            SEntMan.RemoveComponent<RoleplayInfoComponent>(mind);
            SEntMan.RemoveComponent<MindSecretsComponent>(mind);
            Server.System<SLSharedCharacterInfoSystem>().ApplyCharacterInfo(player, new HumanoidCharacterProfile
            {
                PhysicalDescription = "Physical description",
                PersonalityDescription = "Personality description",
                OOCNotes = "OOC notes",
                PersonalNotes = "Personal notes",
            });
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

        var retained = candidates.Keys.First();
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
            var window = FindControls<SLCharacterWindow>(ui.RootControl).Single();
            var tabs = window.FindControl<TabContainer>("CharacterInfoTabs");
            var characterDesc = window.FindControl<CharacterICInfoControl>("InfoIC")
                .FindControl<RichTextLabel>("CharacterDesc");
            var infoOOC = window.FindControl<CharacterOOCInfoControl>("InfoOOC");
            var oocNotes = infoOOC.FindControl<RichTextLabel>("OOCNotes");
            var personalNotes = infoOOC.FindControl<RichTextLabel>("PersonalNotes");
            var background = window.FindControl<CharacterBackgroundInfoControl>("InfoBackground")
                .FindControl<RichTextLabel>("Background");
            var objectivesWrapper = window.FindControl<BoxContainer>("ObjectivesWrapper");
            var cardsButton = window.FindControl<Button>("CardsButton");
            Assert.That(tabs.Children.Count, Is.EqualTo(2));
            Assert.That(tabs.CurrentTab, Is.Zero);
            Assert.That(characterDesc.GetMessage(), Does.Contain("Physical description"));
            Assert.That(characterDesc.GetMessage(), Does.Contain("Personality description"));
            Assert.That(oocNotes.GetMessage(), Does.Contain("OOC notes"));
            Assert.That(personalNotes.GetMessage(), Does.Contain("Personal notes"));
            Assert.That(background.GetMessage(), Does.Contain(Loc.GetString("trait-background-civilian-name")));
            Assert.That(FindControls<Button>(objectivesWrapper).Select(button => button.Text),
                Does.Contain(Loc.GetString("objective-picker-button")),
                "The empty Cards group must not hide the objective picker.");
            Assert.That(cardsButton.Text, Is.EqualTo(Loc.GetString("character-info-cards-button")));
            Assert.That(objectivesWrapper.Children.ElementAt(1), Is.SameAs(cardsButton));

            tabs.CurrentTab = 1;
            window.Close();
            Assert.That(characterDesc.GetMessage(), Is.Null);
            Assert.That(personalNotes.GetMessage(), Is.Null);
            ui.GetUIController<CharacterUIController>().OpenCharacterOverview();
            Assert.That(tabs.CurrentTab, Is.Zero);
            Assert.That(oocNotes.GetMessage(), Does.Contain("OOC notes"));

            ui.GetUIController<ObjectivePickerUIController>().EnsureWindow();
            var picker = FindControls<ObjectivePickerWindow>(ui.RootControl).Single();
            var submit = FindControls<Button>(picker).Single(button => button.Name == "SubmitButton");
            var mulligan = FindControls<Button>(picker).Single(button => button.Name == "MulliganButton");
            Assert.That(submit.Disabled, Is.True);
            Assert.That(mulligan.Disabled, Is.True);
            picker.SelectedObjectives.Add(retained);
            picker.UpdateState();
            Assert.That(submit.Disabled, Is.True, "One under-budget objective cannot be submitted.");
            Assert.That(mulligan.Disabled, Is.False, "Exactly one selection enables the mulligan.");
            picker.Close();

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
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).Objectives, Is.EqualTo(new[] { forced }));
        });

        // An under-budget submission must leave the picker open and assign no offers.
        await Client.WaitAssertion(() => Client.ResolveDependency<IEntityNetworkManager>().SendSystemNetworkMessage(new ObjectivePickerSelected
        {
            MindId = CEntMan.GetNetEntity(Client.System<SharedMindSystem>().GetMind(Client.User!.Value)!.Value),
            SelectedObjectives = new() { retained },
        }));
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<MindComponent>(mind).Objectives, Is.EqualTo(new[] { forced }));
            Assert.That(SEntMan.HasComponent<PotentialObjectivesComponent>(mind), Is.True);
        });

        await Client.WaitAssertion(() => Client.ResolveDependency<IEntityNetworkManager>().SendSystemNetworkMessage(new ObjectivePickerMulligan
        {
            MindId = CEntMan.GetNetEntity(Client.System<SharedMindSystem>().GetMind(Client.User!.Value)!.Value),
            RetainedObjective = retained,
        }));
        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            var offers = SEntMan.GetComponent<PotentialObjectivesComponent>(mind);
            Assert.That(offers.MulliganUsed, Is.True);
            Assert.That(offers.ObjectiveOptions.Keys.Intersect(candidates.Keys), Is.EqualTo(new[] { retained }));
            foreach (var unused in candidates.Where(pair => pair.Key != retained))
                Assert.That(SEntMan.EntityExists(unused.Value), Is.False);
            candidates = offers.ObjectiveOptions.Keys.ToDictionary(id => id, SEntMan.GetEntity);
            Assert.That(ObjectivePickerSelection.TryComplete(offers, offers.ObjectiveOptions.Keys,
                new[] { retained }, out selected), Is.True);
        });

        // A second mulligan must leave the original reroll intact.
        await Client.WaitAssertion(() => Client.ResolveDependency<IEntityNetworkManager>().SendSystemNetworkMessage(new ObjectivePickerMulligan
        {
            MindId = CEntMan.GetNetEntity(Client.System<SharedMindSystem>().GetMind(Client.User!.Value)!.Value),
            RetainedObjective = retained,
        }));
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() => Assert.That(
            SEntMan.GetComponent<PotentialObjectivesComponent>(mind).ObjectiveOptions.Keys, Is.EquivalentTo(candidates.Keys)));

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
                Is.EquivalentTo(selected.Select(objective => candidates[objective]).Append(forced)));
            Assert.That(SEntMan.HasComponent<PotentialObjectivesComponent>(mind), Is.False);
            foreach (var unused in candidates.Where(candidate => !selected.Contains(candidate.Key)))
            {
                Assert.That(SEntMan.EntityExists(unused.Value), Is.False, "Unused offers must be deleted.");
            }
        });

        await Server.WaitAssertion(() =>
        {
            var progress = SEntMan.GetComponent<ObjectivePickerProgressComponent>(mind);
            progress.CanPickMore = true;
            SEntMan.Dirty(mind, progress);
        });
        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
            ui.GetUIController<CharacterUIController>().OpenCharacterOverview());
        await Pair.RunTicksSync(70);
        await Client.WaitAssertion(() =>
        {
            var window = FindControls<SLCharacterWindow>(ui.RootControl).Single();
            var objectivesWrapper = window.FindControl<BoxContainer>("ObjectivesWrapper");
            Assert.That(FindControls<Button>(objectivesWrapper).Select(button => button.Text),
                Does.Contain(Loc.GetString("objective-picker-additional-button")));
            Assert.That(objectivesWrapper.Children.ElementAt(1), Is.SameAs(window.FindControl<Button>("CardsButton")));
            window.Close();
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

            SEntMan.AddComponent<ObjectiveComponent>(futureObjective);
            SEntMan.AddComponent<ObjectiveComponent>(expiredObjective);
            SEntMan.AddComponent<ObjectivePickerConfigurationComponent>(futureMind);
            SEntMan.AddComponent<ObjectivePickerConfigurationComponent>(expiredMind);
            SEntMan.AddComponent(futureMind, new PotentialObjectivesComponent
            {
                AutoSelectionDelay = TimeSpan.FromHours(1),
                MinimumDifficulty = 1,
                Difficulties = new() { [SEntMan.GetNetEntity(futureObjective)] = 1 },
                ObjectiveOptions = new() { [SEntMan.GetNetEntity(futureObjective)] = default },
            });
            SEntMan.AddComponent(expiredMind, new PotentialObjectivesComponent
            {
                AutoSelectionDelay = TimeSpan.Zero,
                MinimumDifficulty = 1,
                Difficulties = new() { [SEntMan.GetNetEntity(expiredObjective)] = 1 },
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

    private static IEnumerable<T> FindControls<T>(Control control) where T : Control
    {
        if (control is T found)
            yield return found;

        foreach (var child in control.Children)
        {
            foreach (var descendant in FindControls<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
