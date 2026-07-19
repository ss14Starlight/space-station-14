using System.Linq;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared._Sol.Medical.Allergy;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Silicons.Borgs.Components;
using Content.Server.Chat.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Allergy;

/// <summary>
/// Mechanical allergy reactions to reagents and foods, seeded from lobby allergy selections.
/// </summary>
public sealed class AllergySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _lastReaction = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AllergyComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.Profile is not HumanoidCharacterProfile profile)
            return;

        if (!CanHaveAllergies(args.Mob))
            return;

        ApplyInnateSpeciesAllergies(args.Mob, profile.Species);

        if (profile.SolAllergies.Count > 0)
        {
            ApplyFromPreferences(args.Mob, profile.SolAllergies);
            return;
        }

        if (profile.CDCharacterRecords is { } records)
            ApplyFromFreeText(args.Mob, records.Allergies, records.DrugAllergies);
    }

    /// <summary>
    /// Biological allergies require organic metabolism. Cyborg chassis and IPC bodies are immune.
    /// </summary>
    public bool CanHaveAllergies(EntityUid target)
    {
        return !HasComp<BorgChassisComponent>(target) &&
               !HasComp<IPCBatteryComponent>(target);
    }

    /// <summary>
    /// Applies the allergies that a species innately carries, at each allergy's default severity.
    /// </summary>
    public void ApplyInnateSpeciesAllergies(EntityUid mob, string species)
    {
        if (!CanHaveAllergies(mob))
            return;

        var innate = new List<CharacterAllergyPreference>();
        foreach (var proto in _prototypes.EnumeratePrototypes<AllergyPrototype>())
        {
            if (proto.InnateSpecies.Any(s => s.Id == species))
                innate.Add(new CharacterAllergyPreference(proto.ID, proto.DefaultSeverity));
        }

        ApplyFromPreferences(mob, innate);

        if (innate.Count == 0 || !TryComp<AllergyComponent>(mob, out var component))
            return;

        foreach (var entry in innate)
            component.InnateAllergies.Add(entry.AllergyId);

        Dirty(mob, component);
    }

    /// <summary>
    /// Applies structured lobby allergy selections onto a mob.
    /// </summary>
    public void ApplyFromPreferences(EntityUid mob, IReadOnlyList<CharacterAllergyPreference> preferences)
    {
        if (preferences.Count == 0 || !CanHaveAllergies(mob))
            return;

        var comp = EnsureComp<AllergyComponent>(mob);
        var dirty = false;

        foreach (var entry in preferences)
        {
            if (!_prototypes.HasIndex(entry.AllergyId))
                continue;

            if (!comp.Allergies.Contains(entry.AllergyId))
            {
                comp.Allergies.Add(entry.AllergyId);
                dirty = true;
            }

            if (!comp.Severities.TryGetValue(entry.AllergyId, out var existing) || existing != entry.Severity)
            {
                comp.Severities[entry.AllergyId] = entry.Severity;
                dirty = true;
            }
        }

        if (dirty)
            Dirty(mob, comp);
    }

    /// <summary>
    /// Maps free-text CD allergy fields onto mechanical allergy prototypes by name/id match.
    /// </summary>
    public void ApplyFromFreeText(EntityUid mob, string allergies, string drugAllergies)
    {
        if (!CanHaveAllergies(mob))
            return;

        var combined = $"{allergies};{drugAllergies}";
        var scrubbed = combined.Replace("None", "", StringComparison.OrdinalIgnoreCase).Trim(';', ' ', '\n', '\t');
        if (string.IsNullOrWhiteSpace(scrubbed))
            return;

        var tokens = combined.Split(new[] { ';', ',', '/', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var preferences = new List<CharacterAllergyPreference>();

        foreach (var token in tokens)
        {
            if (token.Equals("None", StringComparison.OrdinalIgnoreCase))
                continue;

            // Allow "Peanut (Severe)" style tokens produced by the structured editor sync.
            var nameToken = token;
            AllergySeverity? explicitSeverity = null;
            var open = token.LastIndexOf('(');
            var close = token.LastIndexOf(')');
            if (open > 0 && close > open)
            {
                nameToken = token[..open].Trim();
                explicitSeverity = HumanoidCharacterProfile.ParseSeverity(token[(open + 1)..close], AllergySeverity.Mild);
            }

            foreach (var proto in _prototypes.EnumeratePrototypes<AllergyPrototype>())
            {
                var name = Loc.GetString(proto.Name);
                if (!proto.ID.Contains(nameToken, StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains(nameToken, StringComparison.OrdinalIgnoreCase) &&
                    !nameToken.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (preferences.Any(p => p.AllergyId == proto.ID))
                    continue;

                preferences.Add(new CharacterAllergyPreference(proto.ID, explicitSeverity ?? proto.DefaultSeverity));
            }
        }

        ApplyFromPreferences(mob, preferences);
    }

    private void OnIngesting(Entity<AllergyComponent> eater, ref IngestingEvent args)
    {
        CheckFoodAllergy(eater, args.Food, args.Split);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<AllergyComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var allergy, out var bloodstream))
        {
            if (!CanHaveAllergies(uid))
                continue;

            if (_lastReaction.TryGetValue(uid, out var last) && _timing.CurTime < last + TimeSpan.FromSeconds(5))
                continue;

            if (!_solutions.TryGetSolution(uid, bloodstream.BloodSolutionName, out _, out var solution))
                continue;

            foreach (var allergyId in allergy.Allergies)
            {
                if (!_prototypes.TryIndex(allergyId, out AllergyPrototype? proto))
                    continue;

                foreach (var reagent in proto.TriggerReagents)
                {
                    if (solution.GetTotalPrototypeQuantity(reagent) <= 0)
                        continue;

                    TriggerAllergy(uid, allergy, proto);
                    _lastReaction[uid] = _timing.CurTime;
                    return;
                }
            }
        }
    }

    public void CheckFoodAllergy(EntityUid eater, EntityUid food, Solution? swallowed = null)
    {
        if (!CanHaveAllergies(eater) || !TryComp<AllergyComponent>(eater, out var allergy))
            return;

        var foodId = MetaData(food).EntityPrototype?.ID;
        if (foodId == null)
            return;

        foreach (var allergyId in allergy.Allergies)
        {
            if (!_prototypes.TryIndex(allergyId, out AllergyPrototype? proto))
                continue;

            if (!FoodMatchesAllergy(foodId, swallowed, proto))
                continue;

            TriggerAllergy(eater, allergy, proto);
            _lastReaction[eater] = _timing.CurTime;
            return;
        }
    }

    public bool FoodMatchesAllergy(EntProtoId foodId, Solution? swallowed, AllergyPrototype allergy)
    {
        if (swallowed != null && allergy.TriggerReagents.Any(reagent =>
                swallowed.GetTotalPrototypeQuantity(reagent) > 0))
            return true;

        if (allergy.TriggerFoods.Contains(foodId))
            return true;

        return allergy.TriggerFoodRoots.Any(root => IsPrototypeOrDescendant(foodId, root));
    }

    private bool IsPrototypeOrDescendant(EntProtoId foodId, EntProtoId rootId)
    {
        if (foodId == rootId)
            return true;

        var pending = new Stack<EntProtoId>();
        var visited = new HashSet<EntProtoId>();
        pending.Push(foodId);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current) ||
                !_prototypes.TryIndex(current, out EntityPrototype? prototype) ||
                prototype.Parents == null)
            {
                continue;
            }

            foreach (var parent in prototype.Parents)
            {
                if (parent == rootId)
                    return true;
                pending.Push(parent);
            }
        }

        return false;
    }

    public void TriggerAllergy(EntityUid uid, AllergyComponent component, AllergyPrototype allergy)
    {
        if (!CanHaveAllergies(uid))
            return;

        var severity = component.Severities.TryGetValue(allergy.ID, out var chosen)
            ? chosen
            : allergy.DefaultSeverity;

        // Species contraindications already inflict their native reagent-metabolism
        // effects. Avoid stacking generic allergy damage on top of those effects.
        if (!component.InnateAllergies.Contains(allergy.ID))
        {
            switch (severity)
            {
                case AllergySeverity.Mild:
                    if (allergy.MildDamage.GetTotal() > 0)
                        _damageable.TryChangeDamage(uid, allergy.MildDamage, interruptsDoAfters: false);
                    break;
                case AllergySeverity.Moderate:
                    // Moderate is a stronger mild reaction: apply mild damage twice.
                    if (allergy.MildDamage.GetTotal() > 0)
                    {
                        _damageable.TryChangeDamage(uid, allergy.MildDamage, interruptsDoAfters: false);
                        _damageable.TryChangeDamage(uid, allergy.MildDamage, interruptsDoAfters: false);
                    }
                    break;
                case AllergySeverity.Severe:
                case AllergySeverity.Anaphylaxis:
                    if (allergy.SevereDamage.GetTotal() > 0)
                        _damageable.TryChangeDamage(uid, allergy.SevereDamage, interruptsDoAfters: false);
                    break;
            }
        }

        _popup.PopupEntity(Loc.GetString("sol-allergy-reaction", ("allergy", Loc.GetString(allergy.Name))), uid, uid);

        if (!component.InnateAllergies.Contains(allergy.ID) &&
            allergy.CausesSneezing &&
            _random.Prob(0.35f))
            _chat.TryEmoteWithChat(uid, "Sneeze", ChatTransmitRange.GhostRangeLimit);

        if (!component.InnateAllergies.Contains(allergy.ID) &&
            (allergy.CausesAnaphylaxis || severity >= AllergySeverity.Anaphylaxis))
            _popup.PopupEntity(Loc.GetString("sol-allergy-anaphylaxis"), uid, uid, PopupType.LargeCaution);
    }

    public bool HasAllergy(EntityUid uid, ProtoId<AllergyPrototype> allergyId)
    {
        return TryComp<AllergyComponent>(uid, out var comp) &&
               comp.Allergies.Contains(allergyId);
    }

    public IEnumerable<string> GetAllergyDisplayNames(EntityUid uid)
    {
        if (!TryComp<AllergyComponent>(uid, out var comp))
            yield break;

        foreach (var id in comp.Allergies)
        {
            if (_prototypes.TryIndex(id, out AllergyPrototype? proto))
            {
                var severity = comp.Severities.TryGetValue(id, out var chosen)
                    ? chosen
                    : proto.DefaultSeverity;
                yield return $"{Loc.GetString(proto.Name)} ({HumanoidCharacterProfile.FormatSeverity(severity)})";
            }
            else
                yield return id;
        }
    }
}
