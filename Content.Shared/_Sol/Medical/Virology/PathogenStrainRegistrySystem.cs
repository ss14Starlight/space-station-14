using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Round-scoped registry of synthesized custom pathogen strains.
/// </summary>
public sealed partial class PathogenStrainRegistrySystem : EntitySystem
{
    public static readonly ProtoId<PathogenPrototype> CustomBaseId = "SolPathogenCustomBase";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<string, RuntimePathogenStrain> _strains = new();
    private readonly Dictionary<string, PathogenDefinition> _resolved = new();
    private int _nextStrainIndex = 1;

    public IReadOnlyDictionary<string, RuntimePathogenStrain> Strains => _strains;

    public override void Shutdown()
    {
        base.Shutdown();
        Clear();
    }

    public void Clear()
    {
        _strains.Clear();
        _resolved.Clear();
        _nextStrainIndex = 1;
    }

    public bool TryGetStrain(string strainId, out RuntimePathogenStrain? strain)
    {
        return _strains.TryGetValue(strainId, out strain);
    }

    public bool TryResolve(string id, out PathogenDefinition? definition)
    {
        if (_resolved.TryGetValue(id, out definition))
            return true;

        if (_strains.TryGetValue(id, out var strain))
        {
            definition = BuildDefinition(strain);
            _resolved[id] = definition;
            return true;
        }

        if (_prototypes.TryIndex<PathogenPrototype>(id, out var proto) && proto != null)
        {
            definition = PathogenDefinition.FromPrototype(proto);
            return true;
        }

        definition = null;
        return false;
    }

    /// <summary>
    /// Registers a gene-defined custom strain on the blank synthesis base.
    /// </summary>
    public PathogenDefinition RegisterStrain(
        List<ProtoId<PathogenTraitPrototype>> traits,
        EntityUid? creator = null,
        string? codename = null)
    {
        ValidateTraits(traits, maxBudget: 6);

        var strainId = $"SolStrain-{_nextStrainIndex:D3}";
        _nextStrainIndex++;

        var strain = new RuntimePathogenStrain
        {
            StrainId = strainId,
            Codename = codename ?? $"BT-{strainId[^3..]}",
            ChassisId = CustomBaseId,
            Traits = new List<ProtoId<PathogenTraitPrototype>>(traits),
            CreatedAt = _timing.CurTime,
            Creator = creator == null ? null : GetNetEntity(creator.Value),
        };

        _strains[strainId] = strain;
        var definition = BuildDefinition(strain);
        _resolved[strainId] = definition;
        return definition;
    }

    /// <summary>
    /// Builds a non-registered definition for synthesizer forecasting from selected genes.
    /// </summary>
    public PathogenDefinition PreviewDefinition(IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits)
    {
        return BuildDefinitionFromTraits(traits, strainId: "preview", displayName: Loc.GetString("sol-pathogen-synth-ui-forecast-title"));
    }

    public void ValidateTraits(IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits, int maxBudget)
    {
        if (!TryValidateTraits(traits, maxBudget, out var error))
            throw new InvalidOperationException(error ?? "Invalid trait selection");
    }

    public bool TryValidateTraits(
        IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits,
        int maxBudget,
        out string? error)
    {
        var budget = 0;
        var seen = new HashSet<string>();
        foreach (var traitId in traits)
        {
            if (!_prototypes.TryIndex(traitId, out PathogenTraitPrototype? trait) || trait == null)
            {
                error = Loc.GetString("sol-pathogen-synth-error-unknown-trait", ("trait", traitId.ToString()));
                return false;
            }

            var traitName = Loc.GetString(trait.Name);
            if (!seen.Add(trait.ID))
            {
                error = Loc.GetString("sol-pathogen-synth-error-duplicate-trait", ("trait", traitName));
                return false;
            }

            foreach (var other in trait.IncompatibleWith)
            {
                if (!traits.Any(t => t == other))
                    continue;

                var otherName = _prototypes.TryIndex(other, out PathogenTraitPrototype? otherTrait) && otherTrait != null
                    ? Loc.GetString(otherTrait.Name)
                    : other.ToString();
                error = Loc.GetString("sol-pathogen-synth-error-incompatible",
                    ("trait", traitName),
                    ("other", otherName));
                return false;
            }

            foreach (var selected in traits)
            {
                if (selected == traitId)
                    continue;
                if (!_prototypes.TryIndex(selected, out PathogenTraitPrototype? selectedTrait) || selectedTrait == null)
                    continue;
                if (!selectedTrait.IncompatibleWith.Contains(trait.ID))
                    continue;

                error = Loc.GetString("sol-pathogen-synth-error-incompatible",
                    ("trait", traitName),
                    ("other", Loc.GetString(selectedTrait.Name)));
                return false;
            }

            budget += trait.BudgetCost;
        }

        if (budget > maxBudget)
        {
            error = Loc.GetString("sol-pathogen-synth-error-budget",
                ("used", budget),
                ("max", maxBudget));
            return false;
        }

        error = null;
        return true;
    }

    public int GetTraitBudget(IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits)
    {
        var budget = 0;
        foreach (var traitId in traits)
        {
            if (_prototypes.TryIndex(traitId, out PathogenTraitPrototype? trait) && trait != null)
                budget += trait.BudgetCost;
        }

        return budget;
    }

    private PathogenDefinition BuildDefinition(RuntimePathogenStrain strain)
    {
        return BuildDefinitionFromTraits(strain.Traits, strain.StrainId, strain.Codename);
    }

    private PathogenDefinition BuildDefinitionFromTraits(
        IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits,
        string strainId,
        string displayName)
    {
        if (!_prototypes.TryIndex(CustomBaseId, out PathogenPrototype? chassis) || chassis == null)
            throw new InvalidOperationException($"Missing custom synthesis base '{CustomBaseId}'");

        var def = PathogenDefinition.FromPrototype(chassis);
        def.Id = strainId;
        def.DisplayName = displayName;
        def.ChassisId = CustomBaseId;
        def.IsRuntimeStrain = true;
        def.VaccineIdentity = strainId;
        def.TraitIds = traits.Select(t => t.Id).ToList();
        def.Treatments.Clear();

        foreach (var traitId in traits)
        {
            if (!_prototypes.TryIndex(traitId, out PathogenTraitPrototype? trait) || trait == null)
                continue;

            def.Transmission |= trait.AddTransmission;
            def.InfectiveDose = Math.Max(0.1f, def.InfectiveDose * trait.InfectiveDoseMultiplier);
            def.BaseInfectionChance = Math.Clamp(def.BaseInfectionChance + trait.InfectionChanceBonus, 0.01f, 0.95f);
            def.IncubationDuration *= Math.Clamp(trait.IncubationMultiplier, 0.25f, 3f);
            def.SymptomaticDuration *= Math.Clamp(trait.SymptomaticMultiplier, 0.25f, 3f);
            def.Lethality = Math.Clamp(def.Lethality + trait.LethalityBonus, 0f, 0.95f);
            def.EnvironmentalDecayPerSecond = Math.Max(0.001f,
                def.EnvironmentalDecayPerSecond / Math.Max(0.25f, trait.EnvironmentalPersistenceMultiplier));
            def.SterilantSusceptibility = Math.Clamp(
                def.SterilantSusceptibility * trait.SterilantSusceptibilityMultiplier, 0.1f, 3f);
            def.CoughChancePerSecond = Math.Clamp(def.CoughChancePerSecond + trait.CoughChanceBonus, 0f, 0.5f);
            def.SneezeChancePerSecond = Math.Clamp(def.SneezeChancePerSecond + trait.SneezeChanceBonus, 0f, 0.5f);
            def.OrganDamagePerSecond = Math.Max(0f, def.OrganDamagePerSecond + trait.OrganDamageBonus);

            foreach (var organ in trait.AddTargetOrgans)
            {
                if (!def.TargetOrgans.Contains(organ))
                    def.TargetOrgans.Add(organ);
            }

            if (trait.SymptomDamageBonus.DamageDict.Count > 0)
                def.SymptomaticDamage += trait.SymptomDamageBonus;

            foreach (var treatment in trait.AddTreatments)
            {
                if (!def.Treatments.Contains(treatment))
                    def.Treatments.Add(treatment);
            }
        }

        // Hard viability clamp: cannot max every axis at once.
        var routeCount = CountRoutes(def.Transmission);
        if (routeCount >= 3 && def.Lethality > 0.55f && def.BaseInfectionChance > 0.7f)
            def.BaseInfectionChance = 0.55f;

        return def;
    }

    private static int CountRoutes(PathogenTransmission transmission)
    {
        var count = 0;
        if ((transmission & PathogenTransmission.Contact) != 0)
            count++;
        if ((transmission & PathogenTransmission.Airborne) != 0)
            count++;
        if ((transmission & PathogenTransmission.Ingestion) != 0)
            count++;
        if ((transmission & PathogenTransmission.Fluid) != 0)
            count++;
        return count;
    }
}
