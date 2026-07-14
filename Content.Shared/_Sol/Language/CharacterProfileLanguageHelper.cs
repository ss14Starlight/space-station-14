using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared._Starlight.Traits;
using Content.Shared._Starlight.Traits.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sol.Language;

/// <summary>
///     Computes a character's known languages from species + trait preferences
///     without spawning an entity (lobby / character editor).
/// </summary>
public static class CharacterProfileLanguageHelper
{
    private static readonly ProtoId<LanguagePrototype> MachineLanguage = "Machine";

    private static readonly ProtoId<LanguagePrototype>[] ElfEthnicityLanguages =
    [
        "Aielic",
        "Sylvan",
        "Darktongue",
        "Felyaic",
    ];

    public readonly record struct LanguageKnowledgePreview(
        List<ProtoId<LanguagePrototype>> Speaks,
        List<ProtoId<LanguagePrototype>> Understands);

    public static LanguageKnowledgePreview GetLanguages(
        HumanoidCharacterProfile profile,
        IPrototypeManager prototypes,
        IComponentFactory factory)
    {
        var speaks = new List<ProtoId<LanguagePrototype>>();
        var understands = new List<ProtoId<LanguagePrototype>>();

        if (!prototypes.TryIndex<SpeciesPrototype>(profile.Species, out var species)
            || !TryGetInnateLanguages(species.Prototype, prototypes, factory, out var innate))
        {
            return new LanguageKnowledgePreview(speaks, understands);
        }

        speaks.AddRange(innate.Speaks);
        understands.AddRange(innate.Understands);

        // Stable order so results don't flicker between previews.
        foreach (var traitId in profile.TraitPreferences.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            if (!prototypes.TryIndex(traitId, out TraitPrototype? trait))
                continue;

            foreach (var effect in trait.Effects)
            {
                switch (effect)
                {
                    case LanguageEffect languageEffect:
                        ApplyLanguageEffect(speaks, understands, languageEffect);
                        break;
                    case OverrideCompsEffect overrideEffect:
                        ApplyOverrideKnowledge(speaks, understands, overrideEffect);
                        break;
                }
            }

            // Server-only marker comps aren't available client-side; match by trait id.
            switch (trait.ID)
            {
                case "AdoptedOrphan":
                    ApplyAdoptedOrphan(speaks, understands, innate, profile.Species);
                    break;
                case "Foreigner":
                    EnsureForeignerAlternateLanguage(speaks, understands);
                    RemoveLanguage(speaks, understands, SharedLanguageSystem.FallbackLanguagePrototype, true, true);
                    break;
                case "ForeignerLight":
                    EnsureForeignerAlternateLanguage(speaks, understands);
                    RemoveLanguage(speaks, understands, SharedLanguageSystem.FallbackLanguagePrototype, true, false);
                    break;
            }
        }

        return new LanguageKnowledgePreview(speaks, understands);
    }

    /// <summary>
    ///     Matches ForeignerTraitSystem: if the character only speaks Sol Common, grant Galactic Common.
    /// </summary>
    private static void EnsureForeignerAlternateLanguage(
        List<ProtoId<LanguagePrototype>> speaks,
        List<ProtoId<LanguagePrototype>> understands)
    {
        if (speaks.Any(lang => lang != SharedLanguageSystem.FallbackLanguagePrototype))
            return;

        AddLanguage(speaks, understands, "GalacticCommon", true, true);
    }

    /// <summary>
    ///     Resolves <see cref="LanguageKnowledgeComponent"/> from a mob entity prototype,
    ///     walking parents if the flattened registry somehow misses it.
    /// </summary>
    private static bool TryGetInnateLanguages(
        EntProtoId protoId,
        IPrototypeManager prototypes,
        IComponentFactory factory,
        [NotNullWhen(true)] out LanguageKnowledgeComponent? innate)
    {
        innate = null;
        if (!prototypes.TryIndex(protoId, out EntityPrototype? mobProto))
            return false;

        return TryGetInnateLanguages(mobProto, prototypes, factory, out innate);
    }

    private static bool TryGetInnateLanguages(
        EntityPrototype mobProto,
        IPrototypeManager prototypes,
        IComponentFactory factory,
        [NotNullWhen(true)] out LanguageKnowledgeComponent? innate)
    {
        if (mobProto.TryGetComponent(out innate, factory))
            return true;

        if (mobProto.Parents is null)
            return false;

        foreach (var parentId in mobProto.Parents)
        {
            if (prototypes.TryIndex<EntityPrototype>(parentId, out var parent)
                && TryGetInnateLanguages(parent, prototypes, factory, out innate))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyLanguageEffect(
        List<ProtoId<LanguagePrototype>> speaks,
        List<ProtoId<LanguagePrototype>> understands,
        LanguageEffect effect)
    {
        if (effect.RemoveLanguagesSpoken is not null)
        {
            foreach (var lang in effect.RemoveLanguagesSpoken)
                RemoveLanguage(speaks, understands, lang, true, false);
        }

        if (effect.RemoveLanguagesUnderstood is not null)
        {
            foreach (var lang in effect.RemoveLanguagesUnderstood)
                RemoveLanguage(speaks, understands, lang, false, true);
        }

        if (effect.LanguagesSpoken is not null)
        {
            foreach (var lang in effect.LanguagesSpoken)
                AddLanguage(speaks, understands, lang, true, false);
        }

        if (effect.LanguagesUnderstood is not null)
        {
            foreach (var lang in effect.LanguagesUnderstood)
                AddLanguage(speaks, understands, lang, false, true);
        }
    }

    private static void ApplyOverrideKnowledge(
        List<ProtoId<LanguagePrototype>> speaks,
        List<ProtoId<LanguagePrototype>> understands,
        OverrideCompsEffect effect)
    {
        foreach (var (_, entry) in effect.Components)
        {
            if (entry.Component is not LanguageKnowledgeComponent knowledge)
                continue;

            speaks.Clear();
            understands.Clear();
            speaks.AddRange(knowledge.Speaks);
            understands.AddRange(knowledge.Understands);
        }
    }

    private static void ApplyAdoptedOrphan(
        List<ProtoId<LanguagePrototype>> speaks,
        List<ProtoId<LanguagePrototype>> understands,
        LanguageKnowledgeComponent innate,
        ProtoId<SpeciesPrototype> species)
    {
        var toRemove = new HashSet<ProtoId<LanguagePrototype>>();

        foreach (var lang in innate.Speaks)
        {
            if (!IsProtected(lang))
                toRemove.Add(lang);
        }

        foreach (var lang in innate.Understands)
        {
            if (!IsProtected(lang))
                toRemove.Add(lang);
        }

        if (species == "Elf" || species == "ProtoElf")
        {
            foreach (var lang in ElfEthnicityLanguages)
                toRemove.Add(lang);
        }

        foreach (var lang in toRemove)
            RemoveLanguage(speaks, understands, lang, true, true);
    }

    private static bool IsProtected(ProtoId<LanguagePrototype> language)
        => language == SharedLanguageSystem.FallbackLanguagePrototype || language == MachineLanguage;

    private static void AddLanguage(
        List<ProtoId<LanguagePrototype>> speaks,
        List<ProtoId<LanguagePrototype>> understands,
        ProtoId<LanguagePrototype> language,
        bool spoken,
        bool understood)
    {
        if (spoken && !speaks.Contains(language))
            speaks.Add(language);

        if (understood && !understands.Contains(language))
            understands.Add(language);
    }

    private static void RemoveLanguage(
        List<ProtoId<LanguagePrototype>> speaks,
        List<ProtoId<LanguagePrototype>> understands,
        ProtoId<LanguagePrototype> language,
        bool spoken,
        bool understood)
    {
        if (spoken)
            speaks.Remove(language);

        if (understood)
            understands.Remove(language);
    }
}
