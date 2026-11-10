using System.Linq;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Prototypes;
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
    private const string CavitySlot = "cavity";
    private const string ImplantSocketSuffix = "Implant";
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SLBodySystem _slBody = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private MindExamineSystem _mindExamine = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;


    [SubscribeLocalEvent]
    private void OnCollect(Entity<DamageableComponent> patient, ref CollectHealthAnalyzerExtensionsEvent args)
    {
        var hasImplants = TryComp<ImplantedComponent>(patient, out var implanted)
            && implanted.ImplantContainer.ContainedEntities.Count > 0;
        var hasForeignBodies = false;

        if (TryComp<SLBodyComponent>(patient, out var slBody))
        {
            foreach (var (part, _) in _body.GetBodyChildren(patient))
            {
                if (!HasComp<IncisionOpenComponent>(part) &&
                    (!TryComp<SurgeryProgressComponent>(part, out var surgery) || surgery.StartedSurgeries.Count == 0))
                    continue;

                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-open-incisions", "#F79B4A");
                break;
            }

            var found = GetBodyAbnormalities((patient, slBody));

            if (found.MissingOrgans)
                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-missing-organs", "#F74D4A");

            if (found.AbnormalOrgans)
                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-abnormal-organs", "#F7CE4A");

            hasImplants |= found.Implants;
            hasForeignBodies |= found.ForeignBodies;
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

    private sealed class BodyAbnormalities
    {
        public bool MissingOrgans;
        public bool AbnormalOrgans;
        public bool Implants;
        public bool ForeignBodies;
    }

    /// <summary>
    /// Compares the body with the prefab it was built from: organs that are missing or of another kind than it
    /// was built with, implants, and anything that is not an organ where organs go (the cavity included).
    /// </summary>
    private BodyAbnormalities GetBodyAbnormalities(Entity<SLBodyComponent> body)
    {
        var found = new BodyAbnormalities();
        if (_prototypes.TryIndex(body.Comp.Prefab, out var prefab)
            && body.Comp.RootPartEntity is { } root
            && TryComp<SLBodyPartComponent>(root, out var rootComp))
        {
            CollectAbnormalities((root, rootComp), prefab.Root, found);
        }

        return found;
    }

    private void CollectAbnormalities(Entity<SLBodyPartComponent> part, BodyPartDef def, BodyAbnormalities found)
    {
        if (_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(CavitySlot), out var cavity)
            && cavity.ContainedEntities.Count > 0)
            found.ForeignBodies = true;

        foreach (var socketId in part.Comp.Sockets.Keys)
        {
            // A tool or food put where an organ goes sits in the slot's item container.
            if (_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(SharedBodySystem.GetLegacySlotName(socketId)), out var slotItems)
                && slotItems.ContainedEntities.Count > 0)
                found.ForeignBodies = true;

            BodyPartDef? expected = def.ContainedParts != null && def.ContainedParts.TryGetValue(socketId, out var expectedDef) ? expectedDef : null;
            if (!_slBody.TryGetPartInSocket(part, socketId, out var child))
            {
                if (expected != null)
                    found.MissingOrgans = true;
                continue;
            }

            if (socketId.EndsWith(ImplantSocketSuffix, StringComparison.Ordinal))
            {
                found.Implants = true;
                continue;
            }

            if (!_slBody.IsInternal(child))
                continue;

            if (!HasComp<OrganComponent>(child))
                found.ForeignBodies = true;
            else if (expected is { } organDef && !MatchesOrganPrototype(child, organDef.BodyPart))
                found.AbnormalOrgans = true;
        }

        if (def.AttachedParts == null)
            return;

        foreach (var (socketId, childDef) in def.AttachedParts)
        {
            if (!_slBody.TryGetPartInSocket(part, socketId, out var child))
            {
                // A missing limb is not a missing organ, missing eyes are.
                if (_prototypes.Index(childDef.BodyPart).HasComponent<OrganComponent>(Factory))
                    found.MissingOrgans = true;
                continue;
            }

            if (HasComp<OrganComponent>(child))
            {
                if (!MatchesOrganPrototype(child, childDef.BodyPart))
                    found.AbnormalOrgans = true;
                continue;
            }

            CollectAbnormalities(child, childDef, found);
        }
    }

    private bool MatchesOrganPrototype(EntityUid organ, string expectedPrototype)
        => TryComp(organ, out MetaDataComponent? metadata)
            && metadata.EntityPrototype is { } organPrototype
            && organPrototype.ID == expectedPrototype;

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
