using Content.IntegrationTests.Fixtures;
using Content.Shared._Daiquiri.Language;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared.Implants;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Daiquiri;

[TestFixture]
public sealed class LanguageItemsTest : GameTest
{
    private static readonly ProtoId<LanguagePrototype> GalacticCommon = "GalacticCommon";
    private static readonly ProtoId<LanguagePrototype> VoxPidgin = "VoxPidgin";

    [Test]
    public async Task TranslatorTogglesAndUsesPowerCell()
    {
        await Server.WaitAssertion(() =>
        {
            var human = SSpawn("MobHuman");
            var knowledge = SComp<LanguageKnowledgeComponent>(human);
            knowledge.Speaks.Clear();
            knowledge.Speaks.Add(VoxPidgin);
            knowledge.Understands.Clear();
            knowledge.Understands.Add(VoxPidgin);

            var speaker = SEntMan.EnsureComponent<LanguageSpeakerComponent>(human);
            var language = Server.System<SharedLanguageSystem>();
            language.UpdateEntityLanguages((human, speaker));

            Assert.That(speaker.SpokenLanguages, Does.Contain(VoxPidgin));
            Assert.That(speaker.SpokenLanguages, Does.Not.Contain(GalacticCommon));

            var translator = SSpawn("LanguageTranslatorVoxPidgin");
            Assert.That(SEntMan.HasComponent<ItemToggleComponent>(translator));
            Assert.That(SEntMan.HasComponent<PowerCellSlotComponent>(translator));
            Assert.That(SEntMan.HasComponent<HandheldTranslatorComponent>(translator));

            var powerCell = Server.System<PowerCellSystem>();
            Assert.That(powerCell.HasActivatableCharge(translator), "Translator should start with a charged power cell.");

            var containers = Server.System<SharedContainerSystem>();
            var container = containers.EnsureContainer<Container>(human, "test-translator");
            containers.Insert(translator, container);
            Assert.That(SEntMan.HasComponent<HoldsTranslatorComponent>(human));

            var toggle = Server.System<ItemToggleSystem>();
            Assert.That(toggle.TrySetActive(translator, true), "Could not turn the translator on.");

            language.UpdateEntityLanguages((human, speaker));
            Assert.That(speaker.SpokenLanguages, Does.Contain(GalacticCommon));
            Assert.That(speaker.UnderstoodLanguages, Does.Contain(GalacticCommon));

            Assert.That(toggle.TrySetActive(translator, false), "Could not turn the translator off.");
            language.UpdateEntityLanguages((human, speaker));
            Assert.That(speaker.SpokenLanguages, Does.Not.Contain(GalacticCommon));
        });
    }

    [Test]
    public async Task ImplanterPrototypeInjectsLanguage()
    {
        await Server.WaitAssertion(() =>
        {
            var human = SSpawn("MobHuman");
            var knowledge = SComp<LanguageKnowledgeComponent>(human);
            knowledge.Speaks.Clear();
            knowledge.Understands.Clear();

            var speaker = SEntMan.EnsureComponent<LanguageSpeakerComponent>(human);
            var language = Server.System<SharedLanguageSystem>();
            language.UpdateEntityLanguages((human, speaker));

            var implant = SSpawn("LanguageImplantVoxPidgin");
            Assert.That(SEntMan.HasComponent<LanguageImplantComponent>(implant));
            Assert.That(SEntMan.GetComponent<LanguageImplantComponent>(implant).Language, Is.EqualTo(VoxPidgin));

            var ev = new ImplantImplantedEvent(implant, human);
            SEntMan.EventBus.RaiseLocalEvent(implant, ref ev);

            Assert.That(knowledge.Speaks, Does.Contain(VoxPidgin));
            Assert.That(knowledge.Understands, Does.Contain(VoxPidgin));
        });
    }
}
