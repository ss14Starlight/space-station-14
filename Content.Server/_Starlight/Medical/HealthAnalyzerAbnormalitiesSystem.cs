using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Medical;

/// <summary>
/// Adds patient conditions to the analyzer UI state.
/// </summary>
public sealed partial class HealthAnalyzerAbnormalitiesSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private MindExamineSystem _mindExamine = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, CollectHealthAnalyzerExtensionsEvent>(OnCollect);
    }

    private void OnCollect(Entity<DamageableComponent> patient, ref CollectHealthAnalyzerExtensionsEvent args)
    {
        if (TryComp<BodyComponent>(patient, out var body))
        {
            foreach (var (part, _) in _body.GetBodyChildren(patient, body))
            {
                if (!HasComp<IncisionOpenComponent>(part) && (!TryComp<SurgeryProgressComponent>(part, out var surgery) || surgery.StartedSurgeries.Count == 0))
                    continue;

                Add(args.Abnormalities, "starlight-health-analyzer-abnormality-open-incisions", "#F79B4A");
                break;
            }

            if (HasMissingOrgan(patient, body))
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

    private bool HasMissingOrgan(EntityUid patient, BodyComponent body)
    {
        if (body.Prototype is not { } prototypeId || !_prototypes.TryIndex(prototypeId, out var prototype)
                                                  || _body.GetRootPartOrNull(patient, body) is not { } root)
            return false;

        return HasMissingOrgan(root.Entity, root.BodyPart, prototype, prototype.Root);
    }

    private bool HasMissingOrgan(EntityUid part, BodyPartComponent partComp, BodyPrototype prototype, string slotId)
    {
        if (!prototype.Slots.TryGetValue(slotId, out var template))
            return false;

        foreach (var (organSlot, organProto) in template.Organs)
        {
            if (organProto == "null")
                continue;

            if (!_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(organSlot), out var organContainer)
                || organContainer.ContainedEntities.Count == 0)
                return true;
        }

        foreach (var childSlot in partComp.Children.Keys)
        {
            if (!_containers.TryGetContainer(part, SharedBodySystem.GetPartSlotContainerId(childSlot), out var partContainer))
                continue;

            foreach (var child in partContainer.ContainedEntities)
            {
                if (TryComp<BodyPartComponent>(child, out var childComp) && HasMissingOrgan(child, childComp, prototype, childSlot))
                    return true;
            }
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
