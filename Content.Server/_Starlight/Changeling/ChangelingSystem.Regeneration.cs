using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Limbs;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    private void InitializeRegeneration()
        => SubscribeLocalEvent<ChangelingComponent, BodyPartRemovedEvent>(OnChangelingPartRemoved);

    private void OnChangelingPartRemoved(EntityUid uid, ChangelingComponent comp, ref BodyPartRemovedEvent args)
    {
        if (args.Part.Comp.PartType == BodyPartType.Head)
            args.PreventVitalDamage = true;
    }

    private void UpdateRegeneration(EntityUid uid, ChangelingComponent ling,
        ChangelingRegenerationActionComponent action, float seconds)
    {
        var amount = FixedPoint2.New((ling.Evolved ? action.EvolvedHealingPerSecond : action.HealingPerSecond) * seconds);
        HealRegeneratingEntity(uid, amount);
        if (TryComp<BloodstreamComponent>(uid, out var blood))
        {
            _blood.TryModifyBleedAmount((uid, blood), -blood.BleedAmount);
            var fraction = ling.Evolved ? action.EvolvedBloodRestoreFractionPerSecond : action.BloodRestoreFractionPerSecond;
            _blood.TryModifyBloodLevel((uid, blood), blood.BloodReferenceSolution.Volume * fraction * seconds);
        }

        if (!ling.Evolved || HasComp<ChangelingMindspaceComponent>(uid)
            || TryComp<ChangelingColonyMemberComponent>(uid, out var member) && member.IsFragment
            || !TryComp<BodyComponent>(uid, out var body))
            return;

        foreach (var (organ, _) in _body.GetBodyOrgans(uid, body))
            HealRegeneratingEntity(organ, amount);

        if (_timing.CurTime < ling.NextRegrowth)
            return;
        ling.NextRegrowth = _timing.CurTime + action.RegrowthInterval;
        if (ling.Chemicals < action.RegrowthChemicalCost || !RegrowBody(uid, body, out var regrown))
            return;

        UpdateChemicals(uid, ling, -action.RegrowthChemicalCost);
        _popup.PopupEntity(Loc.GetString("changeling-regrown", ("part", Name(regrown))), uid, uid);
    }

    private void HealRegeneratingEntity(EntityUid uid, FixedPoint2 amount)
    {
        if (!HasComp<DamageableComponent>(uid))
            return;
        var healing = new DamageSpecifier();
        foreach (var (type, damage) in _damage.GetAllDamage(uid).DamageDict)
        {
            if (damage > 0)
                healing.DamageDict[type] = -FixedPoint2.Min(damage, amount);
        }
        _damage.TryChangeDamage(uid, healing, ignoreResistances: true, interruptsDoAfters: false);
    }

    private bool RegrowBody(EntityUid uid, BodyComponent body, out EntityUid regrown)
    {
        regrown = default;
        if (body.Prototype is not { } template || !_proto.TryIndex(template, out var blueprint)
            || _body.GetRootPartOrNull(uid, body) is not { } root)
            return false;

        // Walk the species anatomy from its root; body connections include both directions.
        var pending = new Queue<(string Slot, Entity<BodyPartComponent> Part)>();
        var visited = new HashSet<string> { blueprint.Root };
        pending.Enqueue((blueprint.Root, (root.Entity, root.BodyPart)));
        while (pending.TryDequeue(out var current))
        {
            if (!blueprint.Slots.TryGetValue(current.Slot, out var slot))
                continue;
            foreach (var (organSlot, prototype) in slot.Organs)
            {
                // Optional implants and surgery cavities have no native organ.
                if (prototype == "null")
                    continue;
                var existing = GetRegrowthSlotEntity(current.Part, SharedBodySystem.GetOrganContainerId(organSlot));
                if (existing != null && (MetaData(existing.Value).EntityPrototype?.ID == prototype || !IsProsthesis(existing.Value)))
                    continue;
                var organ = Spawn(prototype, Transform(uid).Coordinates);
                if (!_body.CanInsertOrgan(current.Part, organSlot, current.Part.Comp))
                    _body.TryCreateOrganSlot(current.Part, organSlot, out _, current.Part.Comp);
                if (existing is { } old && !_body.RemoveOrgan(old))
                {
                    QueueDel(organ);
                    return false;
                }
                if (existing is { } extracted)
                {
                    var ev = new SurgeryOrganExtracted(uid, current.Part, extracted);
                    RaiseLocalEvent(extracted, ref ev);
                    _transform.SetCoordinates(extracted, Transform(uid).Coordinates);
                }
                if (!_body.InsertOrgan(current.Part, organ, organSlot, current.Part.Comp))
                {
                    QueueDel(organ);
                    return false;
                }
                regrown = organ;
                return true;
            }
            foreach (var childSlot in slot.Connections)
            {
                if (!visited.Add(childSlot) || !blueprint.Slots.TryGetValue(childSlot, out var child)
                    || child.Part is not { } prototype)
                    continue;
                var existing = GetRegrowthSlotEntity(current.Part, SharedBodySystem.GetPartSlotContainerId(childSlot));
                if (existing is { } attached && (MetaData(attached).EntityPrototype?.ID == prototype.Id || !IsProsthesis(attached)))
                {
                    if (TryComp<BodyPartComponent>(attached, out var part))
                        pending.Enqueue((childSlot, (attached, part)));
                    continue;
                }
                var grown = Spawn(prototype, Transform(uid).Coordinates);
                var grownPart = Comp<BodyPartComponent>(grown);
                _body.TryCreatePartSlot(current.Part, childSlot, grownPart.PartType, out _, current.Part.Comp);
                if (existing is { } old && !DetachRegeneratingProsthesis(uid, old))
                {
                    QueueDel(grown);
                    return false;
                }
                var success = TryComp<HumanoidAppearanceComponent>(uid, out var appearance)
                    ? _colonyLimbs.AttachLimb((uid, appearance), childSlot, current.Part, (grown, grownPart))
                    : _body.AttachPart(current.Part, childSlot, grown, current.Part.Comp, grownPart);
                if (!success)
                {
                    QueueDel(grown);
                    return false;
                }
                regrown = grown;
                return true;
            }
        }
        return false;
    }

    private EntityUid? GetRegrowthSlotEntity(EntityUid part, string slot)
        => _colonyContainers.TryGetContainer(part, slot, out var container) && container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0] : null;

    private bool IsProsthesis(EntityUid uid)
    {
        if (HasComp<CustomLimbComponent>(uid))
            return true;
        if (TryComp<FunctionalOrganComponent>(uid, out var organ) && organ.IsCybernetic
            || TryComp<LimbItemDeployerComponent>(uid, out var deployer) && deployer.IsCybernetic)
            return true;
        if (MetaData(uid).EntityPrototype is not { } prototype)
            return false;
        foreach (var (id, _) in _proto.EnumerateAllParents<EntityPrototype>(prototype.ID, includeSelf: true))
        {
            if (id is "PartCyber" or "OrganCyber")
                return true;
        }
        return false;
    }

    private bool DetachRegeneratingProsthesis(EntityUid uid, EntityUid part)
    {
        ChangelingColonyComponent? colony = null;
        if (TryComp<ChangelingColonyMemberComponent>(uid, out var member))
            TryComp(member.Mind, out colony);
        var suppressed = colony?.SuppressAnimation ?? false;
        if (colony != null)
            colony.SuppressAnimation = true;
        try
        {
            return DetachColonyPart(uid, part);
        }
        finally
        {
            if (colony != null)
                colony.SuppressAnimation = suppressed;
        }
    }
}
