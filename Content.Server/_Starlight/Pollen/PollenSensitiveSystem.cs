using Content.Server._Starlight.Scent.Systems;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Eye;
using Content.Server._CD.Records;
using Content.Shared._CD.Records;
using System;
using System.Linq;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;

namespace Content.Server._Starlight.Pollen; // this somehow also didnt get updated

public sealed partial class PollenSensitiveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ScentSystem _scent = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CharacterRecordsSystem _characterRecords = default!;
    [Dependency] private SharedDarkenedVisionSystem _darkenedVision = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;

    private static readonly TimeSpan s_checkInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_sneezeInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_histamineInterval = TimeSpan.FromSeconds(0.75);
    private const string PollenAllergySlowdown = "PollenAllergySlowdown";
    private static readonly ProtoId<ReagentPrototype> s_histamine = "Histamine";

    private readonly HashSet<Entity<ScentMarkerComponent>> _nearbyMarkers = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var sensitiveQuery = EntityQueryEnumerator<PollenSensitiveComponent, TransformComponent, MobStateComponent>();
        while (sensitiveQuery.MoveNext(out var uid, out var sensitive, out var sensitiveTransform, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            if (now >= sensitive.NextAllergyUpdate)
            {
                sensitive.NextAllergyUpdate = now + s_checkInterval;
                UpdateAllergy((uid, sensitive), now);
            }

            if (now < sensitive.NextInteraction)
                continue;

            sensitive.NextInteraction = now + s_checkInterval;

            if (_scent.IsSealed(uid))
                continue;

            _scent.GetNearbyMarkers(sensitiveTransform.Coordinates, sensitive.PollenRange, _nearbyMarkers);
            var containerHere = _scent.GetAirtightContainer(sensitiveTransform);

            foreach (var (_, pollen) in _nearbyMarkers)
            {
                if (!pollen.IsPollen)
                    continue;

                // Only interact with pollen in the same airtight container.
                if (pollen.ContainedIn != containerHere)
                    continue;

                if (!_random.Prob(sensitive.InteractionChance))
                    continue;

                InteractWithPollen((uid, sensitive), pollen.ScentId, isAdvancedPollen: false);
            }
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenSensitiveComponent, GetVisionDarkeningEvent>(OnGetVisionDarkening);
    }

    private void OnGetVisionDarkening(Entity<PollenSensitiveComponent> ent, ref GetVisionDarkeningEvent args)
    {
        var stack = ent.Comp.AllergyStack;

        if (stack < 2f)
            return;

        var progress = MathF.Min((stack - 2f) / 8f, 1f);
        var strength = 0.05f + (0.25f * MathF.Pow(progress, 1.7f));

        args.Strength += strength;
    }

    private bool IsAllergicToPollen(EntityUid uid, string pollenId, bool isAdvancedPollen)
    {
        if (isAdvancedPollen)
            return true;

        if (!TryComp<CharacterRecordKeyStorageComponent>(uid, out var keyStorage))
            return false;

        var records = _characterRecords.QueryRecords(keyStorage.Key.Station);

        if (!records.TryGetValue(keyStorage.Key.Index, out var record) ||
            string.IsNullOrWhiteSpace(record.PRecords.Allergies))
            return false;

        var normalizedPollenId = NormalizePollenId(pollenId);

        return record.PRecords.Allergies
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(allergy => NormalizePollenId(allergy).Equals(normalizedPollenId, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePollenId(string pollenId)
    {
        const string suffix = "Plants";

        return pollenId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? pollenId[..^suffix.Length]
            : pollenId;
    }
    private void InteractWithPollen(Entity<PollenSensitiveComponent> ent, string pollenId, bool isAdvancedPollen)
    {
        // Collectors (Dionas) absorb pollen through PollenCollectorSystem.
        if (HasComp<PollenCollectorComponent>(ent))
            return;

        if (IsAllergicToPollen(ent.Owner, pollenId, isAdvancedPollen))
            AddAllergyStack(ent);
    }

    private void AddAllergyStack(Entity<PollenSensitiveComponent> ent)
    {
        ent.Comp.AllergyStack += ent.Comp.AllergyBuildup;
        ent.Comp.AllergyStack = MathF.Round(ent.Comp.AllergyStack, 2);
        UpdateAllergyEffects(ent);
    }

    private void UpdateAllergy(Entity<PollenSensitiveComponent> ent, TimeSpan now)
    {
        ent.Comp.AllergyStack = MathF.Max(0f, ent.Comp.AllergyStack - ent.Comp.AllergyDecay);
        ent.Comp.AllergyStack = MathF.Round(ent.Comp.AllergyStack, 2);
        UpdateAllergyEffects(ent);

        if (ent.Comp.HistamineActive && now >= ent.Comp.NextHistamine)
        {
            AddHistamine(ent);
            ent.Comp.NextHistamine = now + s_histamineInterval;
        }
        else if (!ent.Comp.HistamineActive)
        {
            ent.Comp.NextHistamine = TimeSpan.Zero;
        }

        if (!ent.Comp.SneezingActive)
        {
            ent.Comp.NextSneeze = TimeSpan.Zero;
            return;
        }

        if (ent.Comp.AllergyStack <= 3f)
        {
            ent.Comp.NextSneeze = TimeSpan.Zero;
            return;
        }

        if (now < ent.Comp.NextSneeze)
            return;

        ent.Comp.NextSneeze = now + s_sneezeInterval;

        bool sneezed;
        if (TryComp(ent.Owner, out SmellerComponent? smeller))
        {
            sneezed = _scent.ForceAllergySneeze((ent.Owner, smeller), smeller.SmokeLockout);
        }
        else
        {
            // No SmellerComponent (e.g. Humans) - still give sneeze feedback.
            sneezed = true;
        }

        if (sneezed)
        {
            _popup.PopupEntity(Loc.GetString("scent-sneeze-allergic"), ent.Owner, ent.Owner, PopupType.Small);
            ent.Comp.AllergyStack = MathF.Max(0f, ent.Comp.AllergyStack - ent.Comp.SneezeAmount);
            ent.Comp.AllergyStack = MathF.Round(ent.Comp.AllergyStack, 2);
        }

        UpdateAllergyEffects(ent);
    }

    private void UpdateAllergyEffects(Entity<PollenSensitiveComponent> ent)
    {
        var stack = ent.Comp.AllergyStack;

        var oldNoseItch = ent.Comp.NoseItchActive;
        var oldSneezing = ent.Comp.SneezingActive;
        var oldSevere = ent.Comp.SevereAllergyActive;

        ent.Comp.NoseItchActive = stack > 2f;
        ent.Comp.SneezingActive = stack > 4f;
        ent.Comp.HistamineActive = stack > 6f;

        if (!ent.Comp.SevereAllergyActive && stack > 8f)
            ent.Comp.SevereAllergyActive = true;
        else if (ent.Comp.SevereAllergyActive && stack <= 5f)
            ent.Comp.SevereAllergyActive = false;

        if (ent.Comp.NoseItchActive && !oldNoseItch)
        {
            _popup.PopupEntity(
                Loc.GetString("pollen-allergy-nose-itch"),
                ent.Owner,
                ent.Owner,
                PopupType.Small);
        }

        if (ent.Comp.SneezingActive && !oldSneezing)
            ent.Comp.NextSneeze = _timing.CurTime + s_sneezeInterval;

        if (!ent.Comp.SneezingActive && oldSneezing)
            ent.Comp.NextSneeze = TimeSpan.Zero;

        if (stack >= 2f)
        {
            var darkenedVision = EnsureComp<DarkenedVisionComponent>(ent.Owner);
            _darkenedVision.UpdateVisionDarkening((ent.Owner, darkenedVision));
        }
        else if (TryComp<DarkenedVisionComponent>(ent.Owner, out var darkenedVision))
        {
            _darkenedVision.UpdateVisionDarkening((ent.Owner, darkenedVision));

            if (darkenedVision.Strength <= 0f)
                RemComp<DarkenedVisionComponent>(ent.Owner);
        }

        if (ent.Comp.SevereAllergyActive)
        {
            _movementMod.TryUpdateMovementSpeedModDuration(
                ent.Owner,
                PollenAllergySlowdown,
                TimeSpan.FromSeconds(1),
                ent.Comp.SevereWalkSpeedModifier,
                ent.Comp.SevereSprintSpeedModifier);
        }
        else if (ent.Comp.HistamineActive)
        {
            _movementMod.TryUpdateMovementSpeedModDuration(
                ent.Owner,
                PollenAllergySlowdown,
                TimeSpan.FromSeconds(1),
                ent.Comp.HistamineWalkSpeedModifier,
                ent.Comp.HistamineSprintSpeedModifier);
        }
    }

    private void AddHistamine(Entity<PollenSensitiveComponent> ent)
    {
        if (!TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream))
            return;

        var solution = new Solution();
        solution.AddReagent(s_histamine, ent.Comp.HistamineAmount);

        _bloodstream.TryAddToBloodstream((ent.Owner, bloodstream), solution);

        ent.Comp.AllergyStack = MathF.Max(
            0f,
            ent.Comp.AllergyStack - ent.Comp.HistamineBuildupReduction);

        ent.Comp.AllergyStack = MathF.Round(ent.Comp.AllergyStack, 2);
        UpdateAllergyEffects(ent);
    }
}
