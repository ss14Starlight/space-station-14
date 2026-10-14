using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Prototypes;

namespace Content.Server._Starlight.Medical.HealthAnalyzer;

/// <summary>
/// Adds patient conditions to the analyzer UI state.
/// </summary>
public sealed partial class HealthAnalyzerAbnormalitiesSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SLBodySystem _slBody = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private MindExamineSystem _mindExamine = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;


    [SubscribeLocalEvent]
    private void OnCollect(Entity<DamageableComponent> patient, ref CollectHealthAnalyzerExtensionsEvent args)
    {
        if (HasComp<SLBodyComponent>(patient))
        {
            foreach (var (part, _) in _body.GetBodyChildren(patient))
            {
                if (!HasComp<IncisionOpenComponent>(part) && (!TryComp<SurgeryProgressComponent>(part, out var surgery) || surgery.StartedSurgeries.Count == 0))
                    continue;

                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-open-incisions", "#F79B4A");
                break;
            }

            if (TryComp<SLBodyComponent>(patient, out var slBody) && HasMissingOrgan((patient, slBody)))
                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-missing-organs", "#F7784A");
        }

        if (TryComp<MindExaminableComponent>(patient, out var mind))
        {
            _mindExamine.RefreshMindStatus((patient.Owner, mind));  // not super happy about this one but this is kind of needed?
            if (mind.State is MindState.Irrecoverable or MindState.Catatonic)
                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-catatonic", "#BA91E8");
        }

        if (HasComp<AbsorbedComponent>(patient))
            Add(args.Abnormalities, "starlight-health-analyzer-abnormality-hollow", "#F74A4A");

        if (TryComp<BloodstreamComponent>(patient, out var blood)
            && blood.BleedAmount > blood.MaxBleedAmount * 0.75f)
            Add(args.Abnormalities, "starlight-health-analyzer-abnormality-extreme-bleeding", "#C94406");
    }

    private bool HasMissingOrgan(Entity<SLBodyComponent> body)
    {
        if (!_prototypes.TryIndex(body.Comp.Prefab, out var prefab)
            || body.Comp.RootPartEntity is not { } root
            || !TryComp<SLBodyPartComponent>(root, out var rootComp))
            return false;

        return HasMissingOrgan((root, rootComp), prefab.Root);
    }

    private bool HasMissingOrgan(Entity<SLBodyPartComponent> part, BodyPartDef def)
    {
        if (def.ContainedParts != null)
        {
            foreach (var socket in def.ContainedParts.Keys)
            {
                if (!_slBody.TryGetPartInSocket(part, socket, out _))
                    return true;
            }
        }

        if (def.AttachedParts == null)
            return false;

        foreach (var (socket, childDef) in def.AttachedParts)
        {
            if (_slBody.TryGetPartInSocket(part, socket, out var child))
            {
                if (HasMissingOrgan(child, childDef))
                    return true;
            }
            else if (_prototypes.Index(childDef.BodyPart).HasComponent<OrganComponent>(Factory))
            {
                // External organs (eyes)
                return true;
            }
            // A missing limb is not a missing organ.
        }

        return false;
    }

    private void Add(List<HealthAnalyzerAbnormalityData> abnormalities, string description, string color) =>
        abnormalities.Add(new HealthAnalyzerAbnormalityData
        {
            Description = Loc.GetString(description),
            Color = Color.FromHex(color),
        });
}
