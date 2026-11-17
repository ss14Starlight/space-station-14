using System.Linq;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Traits.Assorted;
using Content.Shared.Bed.Sleep;
using Content.Server._Starlight.Medical.Limbs;
using Robust.Shared.Timing;
using Content.Shared.Damage.Components;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight;
using Content.Shared._Starlight.Medical.Surgery.Components;

namespace Content.Server._Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
//
//This file is already overloaded with responsibilities,
//it’s time to break its functionality into different systems.
//However, I don’t want to touch the official systems, so I need to come up with extensions for them.
public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LimbSystem _limbSystem = default!;
    [Dependency] private StarlightEntitySystem _entity = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstreamSystem = default!;

    public void InitializeSteps()
    {
        SubscribeLocalEvent<SurgeryStepBleedEffectComponent, SurgeryStepEvent>(OnStepBleedComplete);
        SubscribeLocalEvent<SurgeryClampBleedEffectComponent, SurgeryStepEvent>(OnStepClampBleedComplete);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepEmoteEffectComplete);
        SubscribeLocalEvent<SurgeryStepSpawnEffectComponent, SurgeryStepEvent>(OnStepSpawnComplete);

        SubscribeLocalEvent<SurgeryStepOrganExtractComponent, SurgeryStepEvent>(OnStepOrganExtractComplete);
        SubscribeLocalEvent<SurgeryStepOrganInsertComponent, SurgeryStepEvent>(OnStepOrganInsertComplete);

        SubscribeLocalEvent<SurgeryStepAttachLimbEffectComponent, SurgeryStepEvent>(OnStepAttachComplete);
        SubscribeLocalEvent<SurgeryStepAmputationEffectComponent, SurgeryStepEvent>(OnStepAmputationComplete);

        SubscribeLocalEvent<CustomLimbMarkerComponent, ComponentRemove>(CustomLimbRemoved);

        SubscribeLocalEvent<SurgeryRemoveAccentComponent, SurgeryStepEvent>(OnRemoveAccent);

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IncisionOpenComponent>();
        while (query.MoveNext(out var uid, out var incision))
        {
            if (_timing.CurTime < incision.NextUpdate)
                continue;

            incision.NextUpdate = _timing.CurTime + incision.UpdateInterval;

            if (!TryComp<BodyPartComponent>(uid, out var part) || part.Body is not { } patient)
                continue;

            _bloodstreamSystem.TryModifyBleedAmount(patient, 0.1f);
        }
    }

    private void OnStepAttachComplete(Entity<SurgeryStepAttachLimbEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_entity.TryGetSingleton(args.SurgeryProto, out var surgery)
            || !TryComp<SurgeryLimbSlotConditionComponent>(surgery, out var slotComp))
            return;

        OnStepAttachLimbComplete(ent, slotComp.Slot, ref args);
        if (slotComp.Slot != "head" && args.IsCancelled)
            OnStepAttachItemComplete(ent, slotComp.Slot, ref args);
    }

    private void OnStepBleedComplete(Entity<SurgeryStepBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.Damage == null)
            return;
        var damage = ent.Comp.Damage;
        if (ent.Comp.Damage is not null && TryComp<DamageableComponent>(args.Body, out var comp))
            _damageableSystem.TryChangeDamage(args.Body, damage);
    }

    private void OnStepClampBleedComplete(Entity<SurgeryClampBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
    }

    private void OnStepOrganInsertComplete(Entity<SurgeryStepOrganInsertComponent> ent, ref SurgeryStepEvent args)
    {
        if (args.Tools.Count == 0
            || !(args.Tools.FirstOrDefault() is var organId)
            || !TryComp<BodyPartComponent>(args.Part, out var bodyPart))
            return;

        var containerId = SharedBodySystem.GetOrganContainerId(ent.Comp.Slot);

        if (ent.Comp.Slot == "cavity" && _containers.TryGetContainer(args.Part, containerId, out var container))
        {
            if (!_containers.Insert(organId, container))
            {
                args.IsCancelled = true;
                return;
            }

            ResetOrganExtractionProgress(args.Part, ent.Comp.Slot);
            return;
        }

        if (!TryComp<OrganComponent>(organId, out var organComp))
        {
            // Allow deliberately wrong implants such as tools or food in organ slots.
            if (!_body.CanInsertOrgan(args.Part, ent.Comp.Slot, bodyPart)
                || !_containers.TryGetContainer(args.Part, containerId, out var itemContainer)
                || !_containers.Insert(organId, itemContainer))
            {
                args.IsCancelled = true;
            }
            else
            {
                ResetOrganExtractionProgress(args.Part, ent.Comp.Slot);
            }

            return;
        }

        var part = args.Part;
        var body = args.Body;

        if (!_body.InsertOrgan(part, organId, ent.Comp.Slot, bodyPart, organComp))
        {
            args.IsCancelled = true;
            return;
        }

        ResetOrganExtractionProgress(part, ent.Comp.Slot);

        var ev = new SurgeryOrganImplantationCompleted(body, part, organId);
        RaiseLocalEvent(organId, ref ev);
    }

    private void ResetOrganExtractionProgress(EntityUid part, string slot)
        => ResetOrganProgress(part, slot, resetExtraction: true);

    private void ResetOrganInsertionProgress(EntityUid part, string slot)
        => ResetOrganProgress(part, slot, resetExtraction: false);

    private void ResetOrganProgress(EntityUid part, string slot, bool resetExtraction)
    {
        if (!TryComp<SurgeryProgressComponent>(part, out var progress))
            return;

        var changed = false;
        foreach (var surgeryId in _surgeries)
        {
            if (!_entity.TryGetSingleton(surgeryId, out var surgeryEnt)
                || !TryComp(surgeryEnt, out SurgeryComponent? surgeryComp))
            {
                continue;
            }

            var removesSlot = false;
            foreach (var stepId in surgeryComp.Steps)
            {
                if (!_entity.TryGetSingleton(stepId, out var stepEnt))
                    continue;

                var matchesSlot = resetExtraction
                    ? TryComp(stepEnt, out SurgeryStepOrganExtractComponent? extract) && extract.Slot == slot
                    : TryComp(stepEnt, out SurgeryStepOrganInsertComponent? insert) && insert.Slot == slot;
                if (!matchesSlot)
                {
                    continue;
                }

                removesSlot = true;
                break;
            }

            if (!removesSlot)
                continue;

            changed |= progress.CompletedSurgeries.Remove(surgeryId);
            changed |= progress.StartedSurgeries.Remove(surgeryId);
            foreach (var stepId in surgeryComp.Steps)
                changed |= progress.CompletedSteps.Remove($"{surgeryId}:{stepId}");
        }

        if (changed)
            Dirty(part, progress);
    }

    private void OnStepOrganExtractComplete(Entity<SurgeryStepOrganExtractComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.Organ?.Count != 1) return;

        var type = ent.Comp.Organ.Values.First().Component.GetType();

        var slot = ent.Comp.Slot;
        if (slot != null && _containers.TryGetContainer(args.Part,
                SharedBodySystem.GetOrganContainerId(slot), out var container))
        {
            var (containedEnt, _) = GetOrganExtractionTarget(ent.Comp, container.ContainedEntities);
            if (containedEnt is not { } organEntity)
                return;

            if (TryComp<OrganComponent>(organEntity, out var organ))
            {
                if (!_body.RemoveOrgan(organEntity, organ))
                {
                    args.IsCancelled = true;
                    return;
                }

                var ev = new SurgeryOrganExtracted(args.Body, args.Part, organEntity);
                RaiseLocalEvent(organEntity, ref ev);
            }
            else if (!_containers.Remove(organEntity, container))
            {
                args.IsCancelled = true;
                return;
            }

            ResetOrganInsertionProgress(args.Part, slot);
            return;
        }

        var organs = _body.GetPartOrgans(args.Part, Comp<BodyPartComponent>(args.Part));
        foreach (var organ in organs)
        {
            if (!HasComp(organ.Id, type) || !_body.RemoveOrgan(organ.Id, organ.Component)) continue;

            var ev = new SurgeryOrganExtracted(args.Body, args.Part, organ.Id);
            RaiseLocalEvent(organ.Id, ref ev);

            return;
        }
    }

    private void OnRemoveAccent(Entity<SurgeryRemoveAccentComponent> ent, ref SurgeryStepEvent args)
    {
        foreach (var accent in _accents)
            if (HasComp(args.Body, accent))
                RemCompDeferred(args.Body, accent);
    }

    private void OnStepEmoteEffectComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var isNumb = _statusEffects.HasEffectComp<PainNumbnessStatusEffectComponent>(args.Body)
            || HasComp<PainNumbnessStatusEffectComponent>(args.Body);

        if (isNumb)
            return;

        if (!HasComp<SleepingComponent>(args.Body))
            _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
        else
            _sleeping.TryWaking(args.Body); // If the patient is sleeping without n2o or reagents, wake them up.
    }

    private void OnStepSpawnComplete(Entity<SurgeryStepSpawnEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryComp(args.Body, out TransformComponent? xform))
            SpawnAtPosition(ent.Comp.Entity, xform.Coordinates);
    }

    private void OnStepAttachLimbComplete(Entity<SurgeryStepAttachLimbEffectComponent> _, string slot, ref SurgeryStepEvent args)
        => args.IsCancelled = args.Tools.Count == 0
            || !(args.Tools.FirstOrDefault() is var limdId)
            || !TryComp<BodyPartComponent>(limdId, out var limb)
            || !TryComp(args.Part, out BodyPartComponent? part)
            || !TryComp(args.Body, out HumanoidAppearanceComponent? humanoid)
            || !_limbSystem.AttachLimb((args.Body, humanoid), slot, (args.Part, part), (limdId, limb));

    private void OnStepAttachItemComplete(Entity<SurgeryStepAttachLimbEffectComponent> _, string slot, ref SurgeryStepEvent args)
        => args.IsCancelled = args.Tools.Count == 0
            || !(args.Tools.FirstOrDefault() is var itemId)
            || !TryComp(itemId, out MetaDataComponent? metadata)
            || HasComp<BodyPartComponent>(itemId)
            || !TryComp(args.Part, out BodyPartComponent? limb)
            || !_limbSystem.AttachItem(args.Body, slot, (args.Part, limb), (itemId, metadata));

    private void OnStepAmputationComplete(Entity<SurgeryStepAmputationEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_entity.TryEntity<TransformComponent, HumanoidAppearanceComponent, BodyComponent>(args.Body, out var body)
            || !_entity.TryEntity<TransformComponent, MetaDataComponent, BodyPartComponent>(args.Part, out var limb)
            || _body.GetParentPartAndSlotOrNull(args.Part) is not { } parentSlot)
            return;

        if (_limbSystem.Amputatate(body, limb))
            ResetLimbAttachmentProgress(parentSlot.Parent, parentSlot.Slot);
    }

    private void ResetLimbAttachmentProgress(EntityUid part, string slot)
    {
        if (!TryComp<SurgeryProgressComponent>(part, out var progress))
            return;

        var changed = false;
        foreach (var surgeryId in _surgeries)
        {
            if (!_entity.TryGetSingleton(surgeryId, out var surgeryEnt)
                || !TryComp(surgeryEnt, out SurgeryComponent? surgeryComp)
                || !TryComp(surgeryEnt, out SurgeryLimbSlotConditionComponent? slotCondition)
                || slotCondition.Slot != slot)
                continue;

            var attachesLimb = false;
            foreach (var stepId in surgeryComp.Steps)
            {
                if (!_entity.TryGetSingleton(stepId, out var stepEnt)
                    || !HasComp<SurgeryStepAttachLimbEffectComponent>(stepEnt))
                    continue;

                attachesLimb = true;
                break;
            }

            if (!attachesLimb)
                continue;

            changed |= progress.CompletedSurgeries.Remove(surgeryId);
            changed |= progress.StartedSurgeries.Remove(surgeryId);
            foreach (var stepId in surgeryComp.Steps)
                changed |= progress.CompletedSteps.Remove($"{surgeryId}:{stepId}");
        }

        if (changed)
            Dirty(part, progress);
    }

    [SubscribeLocalEvent]
    private void OnCustomLimbTerminating(Entity<CustomLimbMarkerComponent> ent, ref EntityTerminatingEvent args)
        => RemoveCustomLimb(ent);

    private void CustomLimbRemoved(Entity<CustomLimbMarkerComponent> ent, ref ComponentRemove args)
        => RemoveCustomLimb(ent);

    private void RemoveCustomLimb(Entity<CustomLimbMarkerComponent> ent)
    {
        if (ent.Comp.VirtualPart is not { } virtualPart)
            return;

        ent.Comp.VirtualPart = null;

        if (!TryComp<BodyPartComponent>(virtualPart, out var virtualBodyPart)
            || virtualBodyPart.Body is not { } bodyId
            || TerminatingOrDeleted(bodyId)
            || !_entity.TryEntity<TransformComponent, HumanoidAppearanceComponent, BodyComponent>(bodyId, out var body)
            || !_entity.TryEntity<TransformComponent, MetaDataComponent, BodyPartComponent>(virtualPart, out var limb)
            || _body.GetParentPartAndSlotOrNull(virtualPart) is not { } parentSlot
            || !_limbSystem.Amputatate(body, limb))
        {
            QueueDel(virtualPart);
            return;
        }

        ResetLimbAttachmentProgress(parentSlot.Parent, parentSlot.Slot);
        RefreshUI(bodyId);
    }
}
