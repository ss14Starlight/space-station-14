using Robust.Shared.Containers;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Components;
using System.Linq;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
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
    [Dependency] private SLBodySystem _slBody = default!;
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

            if (!TryComp<SLBodyPartComponent>(uid, out var part) || part.Body is not { } patient)
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
            || !TryComp<SLBodyPartComponent>(args.Part, out var part))
            return;

        // The cavity holds any item, not only organs.
        if (ent.Comp.Slot == "cavity")
        {
            var cavity = _containers.EnsureContainer<ContainerSlot>(args.Part, SharedBodySystem.GetOrganContainerId(ent.Comp.Slot));
            _containers.Insert(organId, cavity);
            return;
        }

        if (!HasComp<OrganComponent>(organId) || !TryComp<SLBodyPartComponent>(organId, out var organPart))
            return;

        // Implantation events come from the organ entering the body.
        var socketId = SharedBodySystem.GetSocketIdForLegacySlot(ent.Comp.Slot);
        if (!part.Sockets.ContainsKey(socketId))
            _slBody.AddSocket((args.Part, part), socketId, BodyPrefabPrototype.DefaultInternalSocket);

        if (!_slBody.TryAttachPart((args.Part, part), socketId, (organId, organPart)))
            args.IsCancelled = true;
    }

    private void OnStepOrganExtractComplete(Entity<SurgeryStepOrganExtractComponent> ent, ref SurgeryStepEvent args)
    {
        if (ent.Comp.Organ?.Count != 1 || !TryComp<SLBodyPartComponent>(args.Part, out var part))
            return;

        var type = ent.Comp.Organ.Values.First().Component.GetType();

        if (ent.Comp.Slot == "cavity")
        {
            if (_containers.TryGetContainer(args.Part, SharedBodySystem.GetOrganContainerId(ent.Comp.Slot), out var cavity))
            {
                foreach (var containedEnt in cavity.ContainedEntities.ToArray())
                {
                    if (HasComp(containedEnt, type))
                        _containers.Remove(containedEnt, cavity);
                }
            }

            return;
        }

        Entity<SLBodyPartComponent>? organ = null;
        if (ent.Comp.Slot != null)
        {
            if (_slBody.TryGetPartInSocket((args.Part, part), SharedBodySystem.GetSocketIdForLegacySlot(ent.Comp.Slot), out var inSlot)
                && HasComp(inSlot, type))
                organ = inSlot;
        }
        else
        {
            foreach (var child in _slBody.GetChildParts((args.Part, part)))
            {
                if (HasComp<OrganComponent>(child) && HasComp(child, type))
                {
                    organ = child;
                    break;
                }
            }
        }

        if (organ is not { } extracted || !_slBody.TryDetachPart(extracted))
            return;

        var ev = new SurgeryOrganExtracted(args.Body, args.Part, extracted);
        RaiseLocalEvent(extracted, ref ev);
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
            || !TryComp<SLBodyPartComponent>(limdId, out var limb)
            || !TryComp(args.Part, out SLBodyPartComponent? part)
            || !TryComp(args.Body, out HumanoidAppearanceComponent? humanoid)
            || !_limbSystem.AttachLimb((args.Body, humanoid), slot, (args.Part, part), (limdId, limb));

    private void OnStepAttachItemComplete(Entity<SurgeryStepAttachLimbEffectComponent> _, string slot, ref SurgeryStepEvent args)
        => args.IsCancelled = args.Tools.Count == 0
            || !(args.Tools.FirstOrDefault() is var itemId)
            || !TryComp(itemId, out MetaDataComponent? metadata)
            || HasComp<SLBodyPartComponent>(itemId)
            || !TryComp(args.Part, out SLBodyPartComponent? limb)
            || !_limbSystem.AttachItem(args.Body, slot, (args.Part, limb), (itemId, metadata));

    private void OnStepAmputationComplete(Entity<SurgeryStepAmputationEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (_entity.TryEntity<TransformComponent, HumanoidAppearanceComponent, SLBodyComponent>(args.Body, out var body)
            && _entity.TryEntity<TransformComponent, MetaDataComponent, SLBodyPartComponent>(args.Part, out var limb))
            _limbSystem.Amputatate(body, limb);
    }

    private void CustomLimbRemoved(Entity<CustomLimbMarkerComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.VirtualPart is null) return;
        QueueDel(ent.Comp.VirtualPart.Value);
    }
}
