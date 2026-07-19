using System.Linq;
using Content.Shared._FarHorizons.Damage;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared._Sol.Medical.Allergy;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Silicons.Borgs.Components;
using Content.Server.Body.Components;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Chat.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Allergy;

/// <summary>
/// Mechanical allergy reactions to reagents and foods, seeded from lobby allergy selections.
/// Severe / anaphylactic reactions apply sustained asphyxiation and block airloss healing
/// while the reaction is active.
/// </summary>
public sealed class AllergySystem : EntitySystem
{
    private static readonly TimeSpan BloodstreamCheckCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReactionTickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SevereDuration = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan AnaphylaxisDuration = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Saturation drained each severe+ tick so breathing recovery cannot keep up.
    /// </summary>
    private const float SevereSaturationDrain = 2.5f;
    private const float AnaphylaxisSaturationDrain = 4f;

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _lastBloodstreamCheck = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AllergyComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<ActiveAllergyReactionComponent, HealModifyEvent>(OnHealModify);
        SubscribeLocalEvent<ActiveAllergyReactionComponent, ComponentShutdown>(OnReactionShutdown);
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

    private void OnHealModify(Entity<ActiveAllergyReactionComponent> ent, ref HealModifyEvent args)
    {
        if (ent.Comp.Severity < AllergySeverity.Severe)
            return;

        // Airway swelling / bronchospasm: asphyxiation (and Airloss group) will not recover
        // until the reaction ends. Dexalin / respirator recovery are blocked.
        var damage = args.Damage;
        var changed = false;

        foreach (var type in damage.DamageDict.Keys.ToList())
        {
            if (type != "Asphyxiation")
                continue;

            if (damage.DamageDict[type] >= FixedPoint2.Zero)
                continue;

            damage.DamageDict[type] = FixedPoint2.Zero;
            changed = true;
        }

        if (changed)
            args.Damage = damage;
    }

    private void OnReactionShutdown(Entity<ActiveAllergyReactionComponent> ent, ref ComponentShutdown args)
    {
        _lastBloodstreamCheck.Remove(ent);
    }

    public override void Update(float frameTime)
    {
        UpdateActiveReactions();
        UpdateBloodstreamTriggers();
    }

    private void UpdateActiveReactions()
    {
        var query = EntityQueryEnumerator<ActiveAllergyReactionComponent>();
        while (query.MoveNext(out var uid, out var reaction))
        {
            if (_timing.CurTime >= reaction.EndsAt)
            {
                RemCompDeferred<ActiveAllergyReactionComponent>(uid);
                continue;
            }

            if (_timing.CurTime < reaction.NextTick)
                continue;

            reaction.NextTick = _timing.CurTime + ReactionTickInterval;
            Dirty(uid, reaction);

            if (!_prototypes.TryIndex(reaction.AllergyId, out AllergyPrototype? allergy))
                continue;

            if (TryComp<AllergyComponent>(uid, out var allergyComp) &&
                allergyComp.InnateAllergies.Contains(reaction.AllergyId))
                continue;

            ApplyReactionTick(uid, allergy, reaction.Severity);
        }
    }

    private void UpdateBloodstreamTriggers()
    {
        var query = EntityQueryEnumerator<AllergyComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var allergy, out var bloodstream))
        {
            if (!CanHaveAllergies(uid))
                continue;

            if (_lastBloodstreamCheck.TryGetValue(uid, out var last) &&
                _timing.CurTime < last + BloodstreamCheckCooldown)
                continue;

            if (!_solutions.TryGetSolution(uid, bloodstream.BloodSolutionName, out _, out var solution))
                continue;

            _lastBloodstreamCheck[uid] = _timing.CurTime;

            foreach (var allergyId in allergy.Allergies)
            {
                if (!_prototypes.TryIndex(allergyId, out AllergyPrototype? proto))
                    continue;

                foreach (var reagent in proto.TriggerReagents)
                {
                    if (solution.GetTotalPrototypeQuantity(reagent) <= 0)
                        continue;

                    TriggerAllergy(uid, allergy, proto);
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

        var innate = component.InnateAllergies.Contains(allergy.ID);

        // Species contraindications already inflict their native reagent-metabolism
        // effects. Avoid stacking generic allergy damage on top of those effects.
        if (!innate)
        {
            switch (severity)
            {
                case AllergySeverity.Mild:
                    if (allergy.MildDamage.GetTotal() > 0)
                        _damageable.TryChangeDamage(uid, allergy.MildDamage, interruptsDoAfters: false);
                    break;
                case AllergySeverity.Moderate:
                    // Stronger mild burst; no sustained airway shutdown.
                    if (allergy.MildDamage.GetTotal() > 0)
                    {
                        _damageable.TryChangeDamage(uid, allergy.MildDamage, interruptsDoAfters: false);
                        _damageable.TryChangeDamage(uid, allergy.MildDamage, interruptsDoAfters: false);
                    }
                    break;
                case AllergySeverity.Severe:
                case AllergySeverity.Anaphylaxis:
                    StartOrExtendReaction(uid, allergy, severity);
                    // Immediate burst so the first second is already dangerous.
                    ApplyReactionTick(uid, allergy, severity);
                    break;
            }
        }

        var symptomMessage = severity switch
        {
            AllergySeverity.Mild => "sol-allergy-symptoms-mild",
            AllergySeverity.Moderate => "sol-allergy-symptoms-moderate",
            AllergySeverity.Severe => "sol-allergy-symptoms-severe",
            AllergySeverity.Anaphylaxis => "sol-allergy-symptoms-anaphylaxis",
            _ => "sol-allergy-symptoms-mild",
        };
        _popup.PopupEntity(
            Loc.GetString(symptomMessage),
            uid,
            uid,
            severity >= AllergySeverity.Severe ? PopupType.LargeCaution : PopupType.SmallCaution);

        if (!innate &&
            allergy.CausesSneezing &&
            _random.Prob(0.35f))
            _chat.TryEmoteWithChat(uid, "Sneeze", ChatTransmitRange.GhostRangeLimit);
    }

    private void StartOrExtendReaction(EntityUid uid, AllergyPrototype allergy, AllergySeverity severity)
    {
        var duration = severity >= AllergySeverity.Anaphylaxis ? AnaphylaxisDuration : SevereDuration;
        var endsAt = _timing.CurTime + duration;

        var reaction = EnsureComp<ActiveAllergyReactionComponent>(uid);
        // Escalate severity if a worse reaction is already running / newly triggered.
        if (severity > reaction.Severity || reaction.AllergyId == default)
            reaction.Severity = severity;

        reaction.AllergyId = allergy.ID;
        reaction.EndsAt = endsAt > reaction.EndsAt ? endsAt : reaction.EndsAt;
        if (reaction.NextTick < _timing.CurTime)
            reaction.NextTick = _timing.CurTime;

        Dirty(uid, reaction);
    }

    private void ApplyReactionTick(EntityUid uid, AllergyPrototype allergy, AllergySeverity severity)
    {
        var damage = severity >= AllergySeverity.Anaphylaxis
            ? allergy.AnaphylaxisDamage
            : allergy.SevereDamage;

        if (damage.GetTotal() > 0)
            _damageable.TryChangeDamage(uid, damage, interruptsDoAfters: false);

        if (TryComp<RespiratorComponent>(uid, out var respirator))
        {
            var drain = severity >= AllergySeverity.Anaphylaxis
                ? AnaphylaxisSaturationDrain
                : SevereSaturationDrain;
            _respirator.UpdateSaturation(uid, -drain, respirator);
        }
    }

    public bool HasAllergy(EntityUid uid, ProtoId<AllergyPrototype> allergyId)
    {
        return TryComp<AllergyComponent>(uid, out var comp) &&
               comp.Allergies.Contains(allergyId);
    }

    public bool IsHavingSevereReaction(EntityUid uid)
    {
        return TryComp<ActiveAllergyReactionComponent>(uid, out var reaction) &&
               reaction.Severity >= AllergySeverity.Severe &&
               _timing.CurTime < reaction.EndsAt;
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
