using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Round-scoped registry of synthesized custom pathogen strains.
/// </summary>
public sealed partial class PathogenStrainRegistrySystem : EntitySystem
{
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

    public PathogenDefinition RegisterStrain(
        ProtoId<PathogenPrototype> chassisId,
        List<ProtoId<PathogenTraitPrototype>> traits,
        EntityUid? creator = null,
        string? codename = null)
    {
        if (!_prototypes.TryIndex(chassisId, out PathogenPrototype? chassis) || chassis == null)
            throw new InvalidOperationException($"Unknown pathogen chassis '{chassisId}'");

        ValidateTraits(traits, maxBudget: 6);

        var strainId = $"SolStrain-{_nextStrainIndex:D3}";
        _nextStrainIndex++;

        var strain = new RuntimePathogenStrain
        {
            StrainId = strainId,
            Codename = codename ?? $"BT-{strainId[^3..]}",
            ChassisId = chassisId,
            Traits = new List<ProtoId<PathogenTraitPrototype>>(traits),
            CreatedAt = _timing.CurTime,
            Creator = creator == null ? null : GetNetEntity(creator.Value),
        };

        _strains[strainId] = strain;
        var definition = BuildDefinition(strain);
        _resolved[strainId] = definition;
        return definition;
    }

    public void ValidateTraits(IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits, int maxBudget)
    {
        var budget = 0;
        var seen = new HashSet<string>();
        foreach (var traitId in traits)
        {
            if (!_prototypes.TryIndex(traitId, out PathogenTraitPrototype? trait) || trait == null)
                throw new InvalidOperationException($"Unknown pathogen trait '{traitId}'");

            if (!seen.Add(trait.ID))
                throw new InvalidOperationException($"Duplicate trait '{trait.ID}'");

            foreach (var other in trait.IncompatibleWith)
            {
                if (traits.Any(t => t == other))
                    throw new InvalidOperationException($"Trait '{trait.ID}' incompatible with '{other}'");
            }

            foreach (var selected in traits)
            {
                if (selected == traitId)
                    continue;
                if (!_prototypes.TryIndex(selected, out PathogenTraitPrototype? selectedTrait) || selectedTrait == null)
                    continue;
                if (selectedTrait.IncompatibleWith.Contains(trait.ID))
                    throw new InvalidOperationException($"Trait '{trait.ID}' incompatible with '{selectedTrait.ID}'");
            }

            budget += trait.BudgetCost;
        }

        if (budget > maxBudget)
            throw new InvalidOperationException($"Trait budget {budget} exceeds maximum {maxBudget}");
    }

    public bool TryValidateTraits(
        IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits,
        int maxBudget,
        out string? error)
    {
        try
        {
            ValidateTraits(traits, maxBudget);
            error = null;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private PathogenDefinition BuildDefinition(RuntimePathogenStrain strain)
    {
        if (!_prototypes.TryIndex(strain.ChassisId, out PathogenPrototype? chassis) || chassis == null)
            throw new InvalidOperationException($"Missing chassis '{strain.ChassisId}'");

        var def = PathogenDefinition.FromPrototype(chassis);
        def.Id = strain.StrainId;
        def.DisplayName = strain.Codename;
        def.ChassisId = chassis.ID;
        def.IsRuntimeStrain = true;
        def.VaccineIdentity = strain.StrainId;
        def.TraitIds = strain.Traits.Select(t => t.Id).ToList();

        foreach (var traitId in strain.Traits)
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
