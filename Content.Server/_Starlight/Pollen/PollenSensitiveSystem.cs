using Content.Server._Starlight.Scent.Systems;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Pollen.Systems;

public sealed partial class PollenSensitiveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ScentSystem _scent = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    private static TimeSpan s_checkInterval = TimeSpan.FromSeconds(1);
    private static TimeSpan s_sneezeInterval = TimeSpan.FromSeconds(10);

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

    private void InteractWithPollen(Entity<PollenSensitiveComponent> ent, string pollenId)
    {
        if (HasComp<PollenCollectorComponent>(ent))
        {
            CollectPollen(ent, pollenId);
            return;
        }

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

        if (ent.Comp.AllergyStack > 6f)
            AddHistamine(ent);

        UpdateAllergyStage(ent);
    }

    private void UpdateAllergy(Entity<PollenSensitiveComponent> ent, TimeSpan now)
    {
        ent.Comp.AllergyStack = MathF.Max(0f, ent.Comp.AllergyStack - ent.Comp.AllergyDecay);
        UpdateAllergyStage(ent);

        if (ent.Comp.AllergyStage != 3)
            return;

        if (ent.Comp.AllergyStack <= 3f)
        {
            ent.Comp.NextSneeze = TimeSpan.Zero;
            return;
        }

        if (now < ent.Comp.NextSneeze)
            return;

        ent.Comp.NextSneeze = now + s_sneezeInterval;
        ent.Comp.AllergyStack = MathF.Max(0f, ent.Comp.AllergyStack - ent.Comp.SneezeAmount);

        if (TryComp(ent.Owner, out SmellerComponent? smeller))
            _scent.ForceSneeze((ent.Owner, smeller), smeller.SmokeLockout);
    }

    private void UpdateAllergyStage(Entity<PollenSensitiveComponent> ent)
    {
        var oldStage = ent.Comp.AllergyStage;

        ent.Comp.AllergyStage = ent.Comp.AllergyStack switch
        {
            <= 2f => 1,
            <= 4f => 2,
            <= 6f => 3,
            <= 8f => 4,
            _ => 5
        };

        if (ent.Comp.AllergyStage == 2 && oldStage < 2)
        {
            _popup.PopupEntity(
                Loc.GetString("pollen-allergy-nose-itch"),
                ent.Owner,
                ent.Owner,
                PopupType.Small);
        }

        if (ent.Comp.AllergyStage == 3 && oldStage < 3)
        {
            ent.Comp.NextSneeze = _timing.CurTime + s_sneezeInterval;
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
    }
}