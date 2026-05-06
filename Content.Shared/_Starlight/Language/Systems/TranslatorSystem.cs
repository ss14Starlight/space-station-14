using System.Linq;
using Content.Shared._Starlight.Language.Components;
using Content.Shared.Examine;
using Content.Shared.Toggleable;
using Content.Shared._Starlight.Language.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using HandheldTranslatorComponent = Content.Shared._Starlight.Language.Components.HandheldTranslatorComponent;

namespace Content.Shared._Starlight.Language.Systems;

public sealed class TranslatorSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly SharedLanguageSystem _language = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandheldTranslatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HandheldTranslatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotInsertedIntoContainerMessage>(OnTranslatorInserted);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntParentChangedMessage>(OnTranslatorParentChanged);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<HandheldTranslatorComponent, ItemToggledEvent>(OnItemToggled);

        SubscribeLocalEvent<IntrinsicTranslatorComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);

        SubscribeLocalEvent<HoldsTranslatorComponent, DetermineEntityLanguagesEvent>(OnProxyDetermineLanguages);
    }

    private void OnMapInit(Entity<HandheldTranslatorComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<ItemToggleComponent>(ent, out var toggle))
        {
            _itemToggle.SetOnActivate((ent.Owner, toggle), ent.Comp.ToggleOnInteract);
            _itemToggle.TrySetActive((ent.Owner, toggle), ent.Comp.Enabled);
        }
    }

    private void OnExamined(Entity<HandheldTranslatorComponent> ent, ref ExaminedEvent args)
    {
        var understoodLanguageNames = ent.Comp.Understood
            .Select(it => Loc.GetString($"language-{it}-name"));
        var spokenLanguageNames = ent.Comp.Spoken
            .Select(it => Loc.GetString($"language-{it}-name"));
        var requiredLanguageNames = ent.Comp.Requires
            .Select(it => Loc.GetString($"language-{it}-name"));

        args.PushMarkup(Loc.GetString("translator-examined-langs-understood", ("languages", string.Join(", ", understoodLanguageNames))));
        args.PushMarkup(Loc.GetString("translator-examined-langs-spoken", ("languages", string.Join(", ", spokenLanguageNames))));

        args.PushMarkup(Loc.GetString(ent.Comp.RequiresAll ? "translator-examined-requires-all" : "translator-examined-requires-any",
            ("languages", string.Join(", ", requiredLanguageNames))));

        args.PushMarkup(Loc.GetString(ent.Comp.Enabled ? "translator-examined-enabled" : "translator-examined-disabled"));
    }

    private void OnTranslatorInserted(Entity<HandheldTranslatorComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.Owner is not { Valid: true } holder || !HasComp<LanguageSpeakerComponent>(holder))
            return;

        var intrinsic = EnsureComp<HoldsTranslatorComponent>(holder);
        intrinsic.Translators.Add(ent);
        Dirty(holder, intrinsic);

        _language.UpdateEntityLanguages(holder);
    }

    private void OnTranslatorParentChanged(Entity<HandheldTranslatorComponent> ent, ref EntParentChangedMessage args)
    {
        if (!HasComp<HoldsTranslatorComponent>(args.OldParent))
            return;

        // Update the translator on the next tick - this is necessary because there's a good chance the removal from a container.
        // Was caused by the player moving the translator within their inventory rather than removing it.
        // If that is not the case, then OnProxyDetermineLanguages will remove this translator from HoldsTranslatorComponent.Translators.
        _language.CallLanguagesUpdate(args.OldParent.Value);
    }

    private void OnPowerCellSlotEmpty(Entity<HandheldTranslatorComponent> translator, ref PowerCellSlotEmptyEvent args)
    {
        _itemToggle.TrySetActive(translator.Owner, false);
    }

    private void OnPowerCellChanged(Entity<HandheldTranslatorComponent> translator, ref PowerCellChangedEvent args)
    {
        var canEnable = !args.Ejected && _powerCell.HasDrawCharge(translator.Owner);
        _itemToggle.TrySetActive(translator.Owner, canEnable);
    }

    private void OnItemToggled(Entity<HandheldTranslatorComponent> translator, ref ItemToggledEvent args)
    {
        var isEnabled = args.Activated;
        translator.Comp.Enabled = isEnabled;
        Dirty(translator);

        _powerCell.SetDrawEnabled(translator.Owner, isEnabled);
        _appearance.SetData(translator, ToggleableVisuals.Enabled, translator.Comp.Enabled);

        if (_containers.TryGetContainingContainer(translator.Owner, out var holderCont)
            && TryComp<LanguageSpeakerComponent>(holderCont.Owner, out var languageComp))
        {
            // The first new spoken language added by this translator, or null
            var firstNewLanguage = translator.Comp.Spoken.FirstOrDefault(it => !languageComp.SpokenLanguages.Contains(it));
            _language.UpdateEntityLanguages(holderCont.Owner);

            // Update the current language of the entity if necessary
            if (isEnabled && translator.Comp.SetLanguageOnInteract && firstNewLanguage is {})
                _language.SetLanguage((holderCont.Owner, languageComp), firstNewLanguage);
        }

        var loc = isEnabled
            ? "translator-component-turnon"
            : "translator-component-shutoff";
        var message = Loc.GetString(loc, ("translator", translator));
        _popup.PopupClient(message, translator, args.User);
    }

    private void OnDetermineLanguages(EntityUid uid, IntrinsicTranslatorComponent component, DetermineEntityLanguagesEvent ev)
    {
        if (!component.Enabled
            || component.LifeStage >= ComponentLifeStage.Removing
            || !TryComp<LanguageKnowledgeComponent>(uid, out var knowledge)
            || !_powerCell.HasActivatableCharge(uid))
            return;

        CopyLanguages(component, ev, knowledge);
    }

    private void OnProxyDetermineLanguages(Entity<HoldsTranslatorComponent> ent, ref DetermineEntityLanguagesEvent ev)
    {
        if (!TryComp<LanguageKnowledgeComponent>(ent, out var knowledge))
            return;

        foreach (var translator in ent.Comp.Translators.ToArray())
        {
            if (!TryComp(translator, out HandheldTranslatorComponent? translatorComp))
                continue;

            if (!translatorComp.Enabled || !_powerCell.HasActivatableCharge(translator))
                continue;

            if (!_containers.TryGetContainingContainer(translator, out var container) ||
                container.Owner != ent.Owner)
            {
                ent.Comp.Translators.RemoveWhere(it => it == translator);
                continue;
            }

            CopyLanguages(translatorComp, ev, knowledge);
        }

        Dirty(ent);
    }

    private void CopyLanguages(BaseTranslatorComponent from, DetermineEntityLanguagesEvent to, LanguageKnowledgeComponent knowledge)
    {
        var addSpoken = CheckLanguagesMatch(from.Requires, knowledge.Speaks, from.RequiresAll);
        var addUnderstood = CheckLanguagesMatch(from.Requires, knowledge.Understands, from.RequiresAll);

        if (addSpoken)
            foreach (var language in from.Spoken)
                to.SpokenLanguages.Add(language);

        if (addUnderstood)
            foreach (var language in from.Understood)
                to.UnderstoodLanguages.Add(language);
    }

    /// <summary>
    ///     Checks whether any OR all required languages are provided. Used for utility purposes.
    /// </summary>
    public bool CheckLanguagesMatch(ICollection<ProtoId<LanguagePrototype>> required, ICollection<ProtoId<LanguagePrototype>> provided, bool requireAll)
    {
        if (required.Count == 0)
            return true;

        return requireAll
            ? required.All(provided.Contains)
            : required.Any(provided.Contains);
    }
}
