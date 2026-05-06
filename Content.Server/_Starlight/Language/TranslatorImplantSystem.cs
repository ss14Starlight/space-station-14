using Content.Shared.Implants.Components;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.Language.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Language;

public sealed class TranslatorImplantSystem : EntitySystem
{
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly TranslatorSystem _translator = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotInsertedIntoContainerMessage>(OnImplant);
        SubscribeLocalEvent<TranslatorImplantComponent, EntGotRemovedFromContainerMessage>(OnDeImplant);
        SubscribeLocalEvent<ImplantedComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
    }

    private void OnImplant(Entity<TranslatorImplantComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ImplanterComponent.ImplantSlotId)
            return;

        var implantee = Transform(ent).ParentUid;
        if (implantee is not { Valid: true } || !TryComp<LanguageKnowledgeComponent>(implantee, out var knowledge))
            return;

        ent.Comp.Enabled = true;
        // To operate an implant, you need to know its required language intrinsically, because like... it connects to your brain or something,
        // so external translators or other implants can't help you operate it.
        ent.Comp.SpokenRequirementSatisfied = _translator.CheckLanguagesMatch(
            ent.Comp.Requires, knowledge.Speaks, ent.Comp.RequiresAll);

        ent.Comp.UnderstoodRequirementSatisfied = _translator.CheckLanguagesMatch(
            ent.Comp.Requires, knowledge.Understands, ent.Comp.RequiresAll);
        Dirty(ent);

        _language.UpdateEntityLanguages(implantee);
    }

    private void OnDeImplant(Entity<TranslatorImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        // Even though the description of this event says it gets raised BEFORE reparenting, that's actually false...
        ent.Comp.Enabled = ent.Comp.SpokenRequirementSatisfied = ent.Comp.UnderstoodRequirementSatisfied = false;
        Dirty(ent);

        if (TryComp<SubdermalImplantComponent>(ent, out var subdermal) && subdermal.ImplantedEntity is { Valid: true} implantee)
            _language.UpdateEntityLanguages(implantee);
    }

    private void OnDetermineLanguages(Entity<ImplantedComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        // TODO: might wanna find a better solution, i just can't come up with something viable
        foreach (var implant in ent.Comp.ImplantContainer.ContainedEntities)
        {
            if (!TryComp<TranslatorImplantComponent>(implant, out var translator) || !translator.Enabled)
                continue;

            if (translator.SpokenRequirementSatisfied)
                foreach (var language in translator.Spoken)
                    args.SpokenLanguages.Add(language);

            if (translator.UnderstoodRequirementSatisfied)
                foreach (var language in translator.Understood)
                    args.UnderstoodLanguages.Add(language);
        }
    }
}
