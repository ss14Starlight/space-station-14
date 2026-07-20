using System.Linq;
using Content.Shared._FarHorizons.Damage;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared._Sol.Medical.Allergy;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Server.Body.Components;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Chat.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Allergy;

/// <summary>
/// Mechanical allergy reactions to reagents and foods, seeded from lobby allergy selections.
/// Severe / anaphylactic reactions apply sustained asphyxiation after a short onset delay,
/// clamp breathing saturation, block airloss healing, and persist until treated or timed out.
/// </summary>
public sealed class AllergySystem : EntitySystem
{
    private static readonly TimeSpan BloodstreamCheckCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReactionTickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SymptomPopupCooldown = TimeSpan.FromSeconds(12);

    /// <summary>IRL-ish lag between tasting an allergen and the airway reaction kicking in.</summary>
    private static readonly TimeSpan IngestOnsetDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// After choking onset begins, wait this long before airloss damage / hard airway clamp.
    /// </summary>
    private static readonly TimeSpan AirlossDamageDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>How often to refresh stutter while struggling to speak through a closed airway.</summary>
    private static readonly TimeSpan SpeechStruggleRefresh = TimeSpan.FromSeconds(3);

    /// <summary>Base remaining-time added per unit of allergen exposure.</summary>
    private const float MildSecondsPerUnit = 5f;
    private const float ModerateSecondsPerUnit = 6f;
    private const float SevereSecondsPerUnit = 8f;
    private const float AnaphylaxisSecondsPerUnit = 12f;

    /// <summary>Max remaining reaction time that exposure can build up to.</summary>
    private static readonly TimeSpan MildMaxRemaining = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ModerateMaxRemaining = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan SevereMaxRemaining = TimeSpan.FromSeconds(150);
    private static readonly TimeSpan AnaphylaxisMaxRemaining = TimeSpan.FromSeconds(100);

    private const float MaxIntensity = 3f;
    private const float IntensityPerUnit = 0.35f;

    private const float SevereSaturationDrain = 0.5f;
    private const float AnaphylaxisSaturationDrain = 1f;

    /// <summary>
    /// Severe keeps full damage in crit (~60s from crit to dead at intensity 1).
    /// Anaphylaxis is reduced so death takes ~45s after crit.
    /// </summary>
    private const float SevereCriticalDamageMultiplier = 1f;
    private const float AnaphylaxisCriticalDamageMultiplier = 0.67f;

    private static readonly ProtoId<AlertPrototype> AllergicChokingAlert = "SolAllergicChoking";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAllergySystem _sharedAllergy = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _lastBloodstreamCheck = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AllergyComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<ActiveAllergyReactionComponent, HealModifyEvent>(OnHealModify);
        SubscribeLocalEvent<ActiveAllergyReactionComponent, ComponentStartup>(OnReactionStartup);
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

    public bool CanHaveAllergies(EntityUid target)
    {
        return !HasComp<BorgChassisComponent>(target) &&
               !HasComp<IPCBatteryComponent>(target);
    }

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

    private void OnReactionStartup(Entity<ActiveAllergyReactionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Severity >= AllergySeverity.Severe)
            _alerts.ShowAlert(ent.Owner, AllergicChokingAlert);

        // Speech struggle / mute apply when onset begins (see UpdateActiveReactions),
        // not immediately on component add — ingest onset is delayed.
    }

    private void OnReactionShutdown(Entity<ActiveAllergyReactionComponent> ent, ref ComponentShutdown args)
    {
        _lastBloodstreamCheck.Remove(ent);
        _alerts.ClearAlert(ent.Owner, AllergicChokingAlert);

        if (ent.Comp.AppliedMute)
            RemCompDeferred<MutedComponent>(ent.Owner);

        if (ent.Comp.AppliedStutter)
            _stutter.DoRemoveStutter(ent.Owner);
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

            // Wait out the onset delay before symptoms / speech struggle.
            if (_timing.CurTime < reaction.DamageStartsAt)
                continue;

            if (!reaction.OnsetPopupShown)
            {
                reaction.OnsetPopupShown = true;
                ApplySpeechStruggle(uid, reaction);
                Dirty(uid, reaction);
                ShowOnsetPopup(uid, reaction.Severity);
            }

            if (_timing.CurTime < reaction.NextTick)
                continue;

            reaction.NextTick = _timing.CurTime + ReactionTickInterval;
            Dirty(uid, reaction);

            // Keep speech struggle up for the duration of the bout.
            if (reaction.Severity >= AllergySeverity.Severe && reaction.Severity < AllergySeverity.Anaphylaxis)
            {
                _stutter.DoStutter(uid, SpeechStruggleRefresh, refresh: true);
                reaction.AppliedStutter = true;
            }
            else if (reaction.Severity >= AllergySeverity.Anaphylaxis && !reaction.AppliedMute)
            {
                ApplySpeechStruggle(uid, reaction);
            }

            if (!_prototypes.TryIndex(reaction.AllergyId, out AllergyPrototype? allergy))
                continue;

            if (TryComp<AllergyComponent>(uid, out var allergyComp) &&
                allergyComp.InnateAllergies.Contains(reaction.AllergyId))
            {
                if (reaction.Severity >= AllergySeverity.Severe && _timing.CurTime >= reaction.AirlossStartsAt)
                    ClampAirway(uid, reaction.Severity);
                continue;
            }

            ApplyReactionTick(uid, allergy, reaction);
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
                    var qty = solution.GetTotalPrototypeQuantity(reagent);
                    if (qty <= 0)
                        continue;

                    var exposure = Math.Clamp(qty.Float() * 0.25f, 0.5f, 4f);
                    TriggerAllergy(uid, allergy, proto, exposure, delayedOnset: false);
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

            if (!_sharedAllergy.FoodMatchesAllergy(foodId, swallowed, proto))
                continue;

            var exposure = _sharedAllergy.GetExposureUnits(swallowed, proto);
            // Sets PendingTasteAllergyName for the taste popup append.
            _sharedAllergy.TryGetIngestAllergyName(eater, food, swallowed, out _);
            TriggerAllergy(eater, allergy, proto, exposure, delayedOnset: true);
            return;
        }
    }

    public bool FoodMatchesAllergy(EntProtoId foodId, Solution? swallowed, AllergyPrototype allergy)
    {
        return _sharedAllergy.FoodMatchesAllergy(foodId, swallowed, allergy);
    }

    /// <param name="exposureUnits">How much allergen was involved; scales duration and intensity.</param>
    /// <param name="delayedOnset">True for eaten food (1.5s lag). False for blood / tests.</param>
    public void TriggerAllergy(
        EntityUid uid,
        AllergyComponent component,
        AllergyPrototype allergy,
        float exposureUnits = 1f,
        bool delayedOnset = false)
    {
        if (!CanHaveAllergies(uid))
            return;

        exposureUnits = Math.Clamp(exposureUnits, 0.5f, 12f);

        var severity = component.Severities.TryGetValue(allergy.ID, out var chosen)
            ? chosen
            : allergy.DefaultSeverity;

        var innate = component.InnateAllergies.Contains(allergy.ID);

        if (!innate)
        {
            // All severities use a remaining-time reaction; mild/moderate tick poison only.
            StartOrExtendReaction(uid, allergy, severity, exposureUnits, delayedOnset);
            if (!delayedOnset &&
                TryComp<ActiveAllergyReactionComponent>(uid, out var reaction) &&
                _timing.CurTime >= reaction.DamageStartsAt)
            {
                ApplyReactionTick(uid, allergy, reaction);
            }
        }
        else if (severity >= AllergySeverity.Severe)
        {
            StartOrExtendReaction(uid, allergy, severity, exposureUnits, delayedOnset);
            if (!delayedOnset)
                ClampAirway(uid, severity);
        }

        // Ingest path uses taste-append instead of a separate popup.
        if (!delayedOnset)
            TryShowSymptomPopup(uid, component, severity);

        if (!innate &&
            allergy.CausesSneezing &&
            _random.Prob(0.35f))
            _chat.TryEmoteWithChat(uid, "Sneeze", ChatTransmitRange.GhostRangeLimit);
    }

    private void TryShowSymptomPopup(EntityUid uid, AllergyComponent component, AllergySeverity severity)
    {
        if (_timing.CurTime < component.NextSymptomPopup)
            return;

        component.NextSymptomPopup = _timing.CurTime + SymptomPopupCooldown;
        Dirty(uid, component);
        ShowOnsetPopup(uid, severity);
    }

    private void ShowOnsetPopup(EntityUid uid, AllergySeverity severity)
    {
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
            _sharedAllergy.GetCautionPopupType(severity));
    }

    private void StartOrExtendReaction(
        EntityUid uid,
        AllergyPrototype allergy,
        AllergySeverity severity,
        float exposureUnits,
        bool delayedOnset)
    {
        GetDurationParams(severity, out var secondsPerUnit, out var maxRemaining);

        var isNew = !HasComp<ActiveAllergyReactionComponent>(uid);
        var reaction = EnsureComp<ActiveAllergyReactionComponent>(uid);

        if (severity > reaction.Severity || reaction.AllergyId == default)
            reaction.Severity = severity;

        GetDurationParams(reaction.Severity, out secondsPerUnit, out maxRemaining);

        // Remaining-time budget: extend from current end (or now), capped at now + max.
        var add = TimeSpan.FromSeconds(secondsPerUnit * exposureUnits);
        var from = reaction.EndsAt > _timing.CurTime ? reaction.EndsAt : _timing.CurTime;
        var maxEnd = _timing.CurTime + maxRemaining;
        var desiredEnd = from + add;
        reaction.EndsAt = desiredEnd < maxEnd ? desiredEnd : maxEnd;

        reaction.AllergyId = allergy.ID;
        reaction.Intensity = Math.Clamp(reaction.Intensity + exposureUnits * IntensityPerUnit, 1f, MaxIntensity);

        if (isNew || reaction.DamageStartsAt == default)
        {
            reaction.DamageStartsAt = delayedOnset
                ? _timing.CurTime + IngestOnsetDelay
                : _timing.CurTime;
            reaction.AirlossStartsAt = reaction.DamageStartsAt + AirlossDamageDelay;
            reaction.NextTick = reaction.DamageStartsAt;
            reaction.OnsetPopupShown = !delayedOnset;
            if (!delayedOnset)
                ApplySpeechStruggle(uid, reaction);
        }
        else if (reaction.AirlossStartsAt == default || reaction.AirlossStartsAt < reaction.DamageStartsAt)
        {
            reaction.AirlossStartsAt = reaction.DamageStartsAt + AirlossDamageDelay;
        }

        if (reaction.NextTick < reaction.DamageStartsAt)
            reaction.NextTick = reaction.DamageStartsAt;

        Dirty(uid, reaction);
        if (reaction.Severity >= AllergySeverity.Severe)
            _alerts.ShowAlert(uid, AllergicChokingAlert);
    }

    private void ApplySpeechStruggle(EntityUid uid, ActiveAllergyReactionComponent reaction)
    {
        if (reaction.Severity >= AllergySeverity.Anaphylaxis)
        {
            EnsureComp<MutedComponent>(uid);
            reaction.AppliedMute = true;
            return;
        }

        if (reaction.Severity >= AllergySeverity.Severe)
        {
            _stutter.DoStutter(uid, SpeechStruggleRefresh, refresh: true);
            reaction.AppliedStutter = true;
        }
    }

    private static void GetDurationParams(
        AllergySeverity severity,
        out float secondsPerUnit,
        out TimeSpan maxRemaining)
    {
        switch (severity)
        {
            case AllergySeverity.Mild:
                secondsPerUnit = MildSecondsPerUnit;
                maxRemaining = MildMaxRemaining;
                break;
            case AllergySeverity.Moderate:
                secondsPerUnit = ModerateSecondsPerUnit;
                maxRemaining = ModerateMaxRemaining;
                break;
            case AllergySeverity.Severe:
                secondsPerUnit = SevereSecondsPerUnit;
                maxRemaining = SevereMaxRemaining;
                break;
            default:
                secondsPerUnit = AnaphylaxisSecondsPerUnit;
                maxRemaining = AnaphylaxisMaxRemaining;
                break;
        }
    }

    private void ApplyReactionTick(EntityUid uid, AllergyPrototype allergy, ActiveAllergyReactionComponent reaction)
    {
        var baseDamage = reaction.Severity switch
        {
            AllergySeverity.Mild => allergy.MildDamage,
            AllergySeverity.Moderate => allergy.MildDamage * 2f,
            AllergySeverity.Severe => allergy.SevereDamage,
            _ => allergy.AnaphylaxisDamage,
        };

        var airlossReady = _timing.CurTime >= reaction.AirlossStartsAt;

        if (baseDamage.GetTotal() > 0)
        {
            var scaled = baseDamage * reaction.Intensity;
            if (_mobState.IsCritical(uid))
            {
                scaled *= reaction.Severity >= AllergySeverity.Anaphylaxis
                    ? AnaphylaxisCriticalDamageMultiplier
                    : SevereCriticalDamageMultiplier;
            }

            // Choking is felt first; airloss damage waits a beat after onset.
            if (!airlossReady)
                scaled = StripAsphyxiation(scaled);

            if (scaled.GetTotal() > 0)
                _damageable.TryChangeDamage(uid, scaled, interruptsDoAfters: false);
        }

        // Don't keep locking a crit patient's airway into hard airloss.
        if (airlossReady
            && reaction.Severity >= AllergySeverity.Severe
            && !_mobState.IsCritical(uid)
            && !_mobState.IsDead(uid))
        {
            ClampAirway(uid, reaction.Severity);
        }
    }

    private static DamageSpecifier StripAsphyxiation(DamageSpecifier damage)
    {
        if (!damage.DamageDict.ContainsKey("Asphyxiation"))
            return damage;

        var copy = new DamageSpecifier(damage);
        copy.DamageDict.Remove("Asphyxiation");
        return copy;
    }

    private void ClampAirway(EntityUid uid, AllergySeverity severity)
    {
        if (!TryComp<RespiratorComponent>(uid, out var respirator))
            return;

        var overThreshold = respirator.Saturation - respirator.SuffocationThreshold;
        if (overThreshold > 0f)
            _respirator.UpdateSaturation(uid, -overThreshold, respirator);

        var drain = severity >= AllergySeverity.Anaphylaxis
            ? AnaphylaxisSaturationDrain
            : SevereSaturationDrain;
        _respirator.UpdateSaturation(uid, -drain, respirator);
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
               _timing.CurTime < reaction.EndsAt &&
               _timing.CurTime >= reaction.DamageStartsAt;
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
