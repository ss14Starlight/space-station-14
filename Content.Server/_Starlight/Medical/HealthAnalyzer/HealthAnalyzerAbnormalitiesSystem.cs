using System.Linq;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.HealthAnalyzer;

/// <summary>
/// Adds patient conditions to the analyzer UI state.
/// </summary>
public sealed partial class HealthAnalyzerAbnormalitiesSystem : EntitySystem
{
    private static readonly EntProtoId _sleepinessStatusEffect = "StatusEffectSleepiness";
    private static readonly bool _enableImplantDisplay = false;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private MindExamineSystem _mindExamine = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, CollectHealthAnalyzerExtensionsEvent>(OnCollect);
    }

    private void OnCollect(Entity<DamageableComponent> patient, ref CollectHealthAnalyzerExtensionsEvent args)
    {
        var hasImplants = TryComp<ImplantedComponent>(patient, out var implanted)
            && implanted.ImplantContainer.ContainedEntities.Count > 0;
        var hasForeignBodies = false;

        if (TryComp<BodyComponent>(patient, out var body))
        {
            foreach (var (part, _) in _body.GetBodyChildren(patient, body))
            {
                if (!HasComp<IncisionOpenComponent>(part) &&
                    (!TryComp<SurgeryProgressComponent>(part, out var surgery) || surgery.StartedSurgeries.Count == 0))
                    continue;

                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-open-incisions", "#F79B4A");
                break;
            }

            // Why do i have to write something like this? This feels horrible, thanks reviewdog.
            var (missingOrgans, abnormalOrgans, bodyHasImplants, bodyHasForeignBodies) = GetBodyAbnormalities(patient, body);

            if (missingOrgans)
                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-missing-organs", "#F74D4A");

            if (abnormalOrgans)
                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-abnormal-organs", "#F7CE4A");

            hasImplants |= bodyHasImplants;
            hasForeignBodies |= bodyHasForeignBodies;
        }

        if (_enableImplantDisplay && hasImplants)
            Add(args.Abnormalities, "starlight-health-analyzer-abnormality-implants", "#7EA9D8");

        if (hasForeignBodies)
            Add(args.Abnormalities, "starlight-health-analyzer-abnormality-foreign-bodies", "#9D4AF7");

        AddSleepAbnormality(patient, args.Abnormalities);

        if (TryComp<MindExaminableComponent>(patient, out var mind))
        {
            _mindExamine.RefreshMindStatus((patient.Owner,
                mind)); // not super happy about this one but this is kind of needed?
            if (mind.State is MindState.Irrecoverable or MindState.Catatonic)
                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-catatonic", "#BA91E8");
        }

        if (HasComp<AbsorbedComponent>(patient))
            Add(args.Abnormalities, "starlight-health-analyzer-abnormality-hollow", "#8A8A8A");

        if (TryComp<BloodstreamComponent>(patient, out var blood)
            && blood.BleedAmount > blood.MaxBleedAmount * 0.75f)
            Add(args.Abnormalities, "starlight-health-analyzer-abnormality-extreme-bleeding", "#CB0629");
    }

    private (bool MissingOrgans, bool AbnormalOrgans, bool HasImplants, bool HasForeignBodies)
        GetBodyAbnormalities(EntityUid patient, BodyComponent body)
    {
        if (body.Prototype is not { } prototypeId
            || !_prototypes.TryIndex(prototypeId, out var prototype)
            || _body.GetRootPartOrNull(patient, body) is not { } root)
            return (false, false, false, false);

        return GetBodyAbnormalities(root.Entity, root.BodyPart, prototype, prototype.Root);
    }

    private (bool MissingOrgans, bool AbnormalOrgans, bool HasImplants, bool HasForeignBodies)
        GetBodyAbnormalities(EntityUid part,
            BodyPartComponent partComp, BodyPrototype prototype, string slotId)
    {
        var hasImplants = false;
        var hasForeignBodies = false;
        if (!prototype.Slots.TryGetValue(slotId, out var template))
            return (false, false, hasImplants, hasForeignBodies);

        var missing = false;
        var abnormal = false;
        foreach (var (organSlot, organProto) in template.Organs)
        {
            if (organSlot == "cavity")
            {
                if (_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(organSlot), out var cavityContainer)
                    && cavityContainer.ContainedEntities.Count > 0)
                    hasForeignBodies = true;

                continue;
            }

            if (organSlot.EndsWith("_implant", StringComparison.Ordinal))
            {
                if (_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(organSlot), out var implantContainer)
                    && implantContainer.ContainedEntities.Count > 0)
                    hasImplants = true;

                continue;
            }

            if (organProto == "null")
            {
                if (_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(organSlot),
                        out var optionalOrganContainer))
                {
                    if (optionalOrganContainer.ContainedEntities.Any(containedEntity => !HasComp<OrganComponent>(containedEntity)))
                    {
                        hasForeignBodies = true;
                    }
                }

                continue;
            }

            if (!_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(organSlot),
                    out var organContainer))
            {
                missing = true;
                continue;
            }

            var hasOrgan = false;
            foreach (var organ in organContainer.ContainedEntities)
            {
                if (!HasComp<OrganComponent>(organ))
                {
                    hasForeignBodies = true;
                    continue;
                }

                hasOrgan = true;
                if (!MatchesOrganPrototype(organ, organProto))
                    abnormal = true;
            }

            if (!hasOrgan)
                missing = true;
        }

        foreach (var childSlot in partComp.Children.Keys)
        {
            if (!_containers.TryGetContainer(part, SharedBodySystem.GetPartSlotContainerId(childSlot),
                    out var partContainer))
                continue;

            foreach (var child in partContainer.ContainedEntities)
            {
                if (!TryComp<BodyPartComponent>(child, out var childComp))
                    continue;

                var (childMissingOrgans, childAbnormalOrgans, childHasImplants, childHasForeignBodies) =
                    GetBodyAbnormalities(child, childComp, prototype, childSlot);
                missing |= childMissingOrgans;
                abnormal |= childAbnormalOrgans;
                hasImplants |= childHasImplants;
                hasForeignBodies |= childHasForeignBodies;
            }
        }

        return (missing, abnormal, hasImplants, hasForeignBodies);
    }

    private bool MatchesOrganPrototype(EntityUid organ, string expectedPrototype)
    {
        if (!HasComp<OrganComponent>(organ)
            || !TryComp(organ, out MetaDataComponent? metadata)
            || metadata.EntityPrototype is not { } organPrototype)
            return false;

        return organPrototype.ID == expectedPrototype;
    }

    private void AddSleepAbnormality(EntityUid patient, List<HealthAnalyzerAbnormalityData> abnormalities)
    {
        if (!HasComp<SleepingComponent>(patient))
            return;

        if (!_statusEffects.TryGetTime(patient, _sleepinessStatusEffect, out var sleepinessEffect)
            || sleepinessEffect.EndEffectTime is not { } endTime)
        {
            Add(abnormalities, "starlight-health-analyzer-abnormality-unconscious", "#3900AA");
            return;
        }

        var remainingSleepiness = endTime - _timing.CurTime;
        if (remainingSleepiness < TimeSpan.Zero)
            remainingSleepiness = TimeSpan.Zero;

        if (remainingSleepiness <= TimeSpan.FromSeconds(30))
            Add(abnormalities, "starlight-health-analyzer-abnormality-sleeping", "#7B38FF");
        else
            Add(abnormalities, "starlight-health-analyzer-abnormality-anesthetized", "#4D00E6");
    }

    private void Add(List<HealthAnalyzerAbnormalityData> abnormalities, string description, string color) =>
        abnormalities.Add(new HealthAnalyzerAbnormalityData
        {
            Description = Loc.GetString(description),
            Color = Color.FromHex(color),
        });
}
