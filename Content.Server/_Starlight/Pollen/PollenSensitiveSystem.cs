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

namespace Content.Server._Starlight.Pollen.Systems;

public sealed partial class PollenSensitiveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ScentSystem _scent = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private CharacterRecordsSystem _characterRecords = default!;

    private static TimeSpan s_checkInterval = TimeSpan.FromSeconds(1);
    private static TimeSpan s_sneezeInterval = TimeSpan.FromSeconds(10);

    private static TimeSpan s_histamineInterval = TimeSpan.FromSeconds(0.25);

    private static ProtoId<ReagentPrototype> s_histamine = "Histamine";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var sensitiveQuery = EntityQueryEnumerator<PollenSensitiveComponent, TransformComponent>();
        while (sensitiveQuery.MoveNext(out var uid, out var sensitive, out var sensitiveTransform))
        {
            if (now >= sensitive.NextAllergyUpdate)
            {
                sensitive.NextAllergyUpdate = now + s_checkInterval;
                UpdateAllergy((uid, sensitive), now);
            }

            if (now < sensitive.NextInteraction)
                continue;

            sensitive.NextInteraction = now + s_checkInterval;

            var pollenQuery = EntityQueryEnumerator<ScentMarkerComponent, TransformComponent>();
            while (pollenQuery.MoveNext(out _, out var pollen, out var pollenTransform))
            {
                if (!pollen.IsPollen)
                    continue;

                if (!_transform.InRange(sensitiveTransform.Coordinates, pollenTransform.Coordinates, sensitive.PollenRange))
                    continue;

                if (!_random.Prob(sensitive.InteractionChance))
                    continue;

                InteractWithPollen((uid, sensitive), pollen.ScentId);
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
        if (ent.Comp.SevereAllergyActive)
            args.Strength += 0.2f;
    }

    private bool IsAllergicToPollen(EntityUid uid, string pollenId)
    {
        if (!TryComp<CharacterRecordKeyStorageComponent>(uid, out var keyStorage))
            return false;

        var records = _characterRecords.QueryRecords(keyStorage.Key.Station);

        if (!records.TryGetValue(keyStorage.Key.Index, out var record))
            return false;

        if (string.IsNullOrWhiteSpace(record.PRecords.Allergies))
            return false;

        return record.PRecords.Allergies
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(allergy => allergy.Equals(pollenId, StringComparison.OrdinalIgnoreCase));
    }

    private void InteractWithPollen(Entity<PollenSensitiveComponent> ent, string pollenId)
    {
        if (HasComp<PollenCollectorComponent>(ent))
        {
            CollectPollen(ent, pollenId);
            return;
        }

        if (IsAllergicToPollen(ent.Owner, pollenId))
            AddAllergyStack(ent);
    }

    private void CollectPollen(Entity<PollenSensitiveComponent> ent, string pollenId)
    {
        if (!TryComp<PollenCollectorComponent>(ent, out var collector))
            return;

        if (collector.CollectedPollen.Contains(pollenId))
            return;

        collector.CollectedPollen.Add(pollenId);
        Dirty(ent, collector);
    }

    private void AddAllergyStack(Entity<PollenSensitiveComponent> ent)
    {
        ent.Comp.AllergyStack += ent.Comp.AllergyBuildup;
        ent.Comp.AllergyStack = MathF.Round(ent.Comp.AllergyStack, 2);
        UpdateAllergyEffects(ent);

        if (ent.Comp.HistamineActive && _timing.CurTime >= ent.Comp.NextHistamine)
        {
            AddHistamine(ent);
            ent.Comp.NextHistamine = _timing.CurTime + s_histamineInterval;
        }
    }

    private void UpdateAllergy(Entity<PollenSensitiveComponent> ent, TimeSpan now)
    {
        ent.Comp.AllergyStack = MathF.Max(0f, ent.Comp.AllergyStack - ent.Comp.AllergyDecay);
        ent.Comp.AllergyStack = MathF.Round(ent.Comp.AllergyStack, 2);
        UpdateAllergyEffects(ent);

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
        ent.Comp.AllergyStack = MathF.Max(0f, ent.Comp.AllergyStack - ent.Comp.SneezeAmount);
        ent.Comp.AllergyStack = MathF.Round(ent.Comp.AllergyStack, 2);

        if (TryComp(ent.Owner, out SmellerComponent? smeller))
        {
            _scent.ForceAllergySneeze((ent.Owner, smeller), smeller.SmokeLockout);

            _popup.PopupEntity(
                Loc.GetString("scent-sneeze-allergic"),
                ent.Owner,
                ent.Owner,
                PopupType.Small);
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

        if (ent.Comp.SevereAllergyActive && !oldSevere)
            EnsureComp<DarkenedVisionComponent>(ent.Owner);

        if (!ent.Comp.SevereAllergyActive && oldSevere)
            RemComp<DarkenedVisionComponent>(ent.Owner);
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