using Content.Server._Starlight.Language;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Systems;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sol.Traits.Assorted;

public sealed partial class AdoptedOrphanTraitSystem : EntitySystem
{
    private static readonly ProtoId<LanguagePrototype> MachineLanguage = "Machine";

    /// <summary>
    ///     Elf ethnicity traits grant these as mother tongues; species YAML alone does not list them.
    /// </summary>
    private static readonly ProtoId<LanguagePrototype>[] ElfEthnicityLanguages =
    [
        "Aielic",
        "Sylvan",
        "Darktongue",
        "Felyaic",
    ];

    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly LanguageSystem _languages = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
        => SubscribeLocalEvent<AdoptedOrphanTraitComponent, ComponentInit>(OnInit);

    private void OnInit(Entity<AdoptedOrphanTraitComponent> entity, ref ComponentInit args)
    {
        // Defer so ethnicity LanguageEffects applied in the same trait pass finish first.
        var uid = entity.Owner;
        Timer.Spawn(TimeSpan.Zero, () =>
        {
            if (!Exists(uid) || !HasComp<AdoptedOrphanTraitComponent>(uid))
                return;

            StripSpeciesLanguages(uid);
        });
    }

    private void StripSpeciesLanguages(EntityUid uid)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
            || !_proto.TryIndex(humanoid.Species, out SpeciesPrototype? species)
            || !_proto.TryIndex(species.Prototype, out EntityPrototype? mobProto)
            || !mobProto.TryGetComponent(out LanguageKnowledgeComponent? innate, _factory))
        {
            Log.Warning($"Could not resolve innate languages for Adopted Orphan on {ToPrettyString(uid)}");
            return;
        }

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

        if (humanoid.Species == "Elf" || humanoid.Species == "ProtoElf")
        {
            foreach (var lang in ElfEthnicityLanguages)
                toRemove.Add(lang);
        }

        foreach (var lang in toRemove)
            _languages.RemoveLanguage(uid, lang, true, true);
    }

    private static bool IsProtected(ProtoId<LanguagePrototype> language)
        => language == SharedLanguageSystem.FallbackLanguagePrototype || language == MachineLanguage;
}
