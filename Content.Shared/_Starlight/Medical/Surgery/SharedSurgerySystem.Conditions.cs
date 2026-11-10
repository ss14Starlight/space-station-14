using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Body.Components;
using Content.Shared.Humanoid;
using System.Linq;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Surgery.Components;

namespace Content.Shared._Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public abstract partial class SharedSurgerySystem
{
    protected List<Type> _accents = [];
    private void InitializeConditions()
    {
        _accents = _reflectionManager.FindTypesWithAttribute<RegisterComponentAttribute>()
            .Where(type => type.Name.EndsWith("AccentComponent"))
            .ToList();

        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgerySpeciesConditionComponent, SurgeryValidEvent>(OnSpeciesConditionValid);
        SubscribeLocalEvent<SurgeryOrganExistConditionComponent, SurgeryValidEvent>(OnOrganExistConditionValid);
        SubscribeLocalEvent<SurgeryOrganDontExistConditionComponent, SurgeryValidEvent>(OnOrganDontExistConditionValid);
        SubscribeLocalEvent<SurgeryAnyAccentConditionComponent, SurgeryValidEvent>(OnAnyAccentConditionValid);
        SubscribeLocalEvent<SurgeryAnyLimbSlotConditionComponent, SurgeryValidEvent>(OnAnyLimbSlotConditionValid);
        SubscribeLocalEvent<SurgeryLimbSlotConditionComponent, SurgeryValidEvent>(OnLimbSlotConditionValid);
        SubscribeLocalEvent<SurgeryHasCompConditionComponent, SurgeryValidEvent>(OnHasCompConditionValid);
    }

    private void OnHasCompConditionValid(Entity<SurgeryHasCompConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Component == null)
            return; // nothing to check

        foreach (var comp in (ent.Comp.Component ?? []).Values)
            if (!HasComp(args.Body, comp.Component.GetType()))
            {
                args.Cancelled = true;
                return;
            }
    }

    private void OnOrganDontExistConditionValid(Entity<SurgeryOrganDontExistConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Organ?.Count != 1) return;
        var type = ent.Comp.Organ.Values.First().Component.GetType();

        if (ent.Comp.Container != null)
        {
            if (HasOrganInSlot(args.Part, ent.Comp.Container, type))
                args.Cancelled = true;
            return;
        }

        foreach (var organ in _body.GetPartOrgans(args.Part))
        {
            if (HasComp(organ.Id, type))
            {
                args.Cancelled = true;
                return;
            }
        }
    }

    private void OnOrganExistConditionValid(Entity<SurgeryOrganExistConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Organ?.Count != 1) return;

        var type = ent.Comp.Organ.Values.First().Component.GetType();

        EntityUid mainPart = args.Part;

        if (HasComp<SLBodyPartComponent>(args.Body))
            mainPart = args.Body;

        if (ent.Comp.Container != null)
        {
            // Something to take out: the organ, or anything that ended up where it goes.
            if (!HasOrganSlot(mainPart, ent.Comp.Container) || GetOrganSlotContents(mainPart, ent.Comp.Container).Count == 0)
                args.Cancelled = true;
            return;
        }

        foreach (var organ in _body.GetPartOrgans(mainPart))
        {
            if (HasComp(organ.Id, type))
                return;
        }

        args.Cancelled = true;
    }

    /// <summary>
    /// Legacy slot names map to SLBody sockets ("heart" -> "Heart"); the cavity is a plain item container.
    /// </summary>
    /// <summary>
    /// The part has the organ slot and nothing is in it.
    /// </summary>
    public bool CanInsertIntoOrganSlot(EntityUid part, string slot)
        => HasOrganSlot(part, slot) && GetOrganSlotContents(part, slot).Count == 0;

    public bool HasOrganSlot(EntityUid part, string slot)
        => slot == "cavity"
            ? _containers.HasContainer(part, SharedBodySystem.GetOrganContainerId(slot), null)
            : TryComp<SLBodyPartComponent>(part, out var slPart) && slPart.Sockets.ContainsKey(SharedBodySystem.GetSocketIdForLegacySlot(slot));

    /// <summary>
    /// Everything in an organ slot of the part: the organ in its socket, and items in the slot's container (the
    /// cavity, or a tool or food deliberately put where an organ goes).
    /// </summary>
    public List<EntityUid> GetOrganSlotContents(EntityUid part, string slot)
    {
        var contents = new List<EntityUid>();
        if (slot != "cavity"
            && TryComp<SLBodyPartComponent>(part, out var slPart)
            && _slBody.TryGetPartInSocket((part, slPart), SharedBodySystem.GetSocketIdForLegacySlot(slot), out var organ))
            contents.Add(organ);

        if (_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(slot), out var container))
            contents.AddRange(container.ContainedEntities);

        return contents;
    }

    private bool HasOrganInSlot(EntityUid part, string slot, Type type)
    {
        if (slot == "cavity")
        {
            if (!_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(slot), out var cavity))
                return false;
            foreach (var contained in cavity.ContainedEntities)
            {
                if (HasComp(contained, type))
                    return true;
            }
            return false;
        }

        return TryComp<SLBodyPartComponent>(part, out var slPart)
            && _slBody.TryGetPartInSocket((part, slPart), SharedBodySystem.GetSocketIdForLegacySlot(slot), out var organ)
            && HasComp(organ, type);
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Parts.Count == 0)
            return;

        if (TryComp<SLBodyPartComponent>(args.Body, out var itemPart) && itemPart.Kind is BodyPartType item && !ent.Comp.Parts.Contains(item))
        {
            Log.Warning("don't have part at part");
            args.Cancelled = true;
        }

        if (CompOrNull<SLBodyPartComponent>(args.Part)?.Kind is BodyPartType part && !ent.Comp.Parts.Contains(part))
            args.Cancelled = true;
    }
    private void OnSpeciesConditionValid(Entity<SurgerySpeciesConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(args.Body, out var humanoidAppearanceComponent))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.SpeciesBlacklist.Contains(humanoidAppearanceComponent.Species))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.SpeciesWhitelist.Count > 0 && !ent.Comp.SpeciesWhitelist.Contains(humanoidAppearanceComponent.Species))
        {
            args.Cancelled = true;
            return;
        }
    }
    private void OnAnyAccentConditionValid(Entity<SurgeryAnyAccentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        foreach (var accent in _accents)
            if (HasComp(args.Body, accent))
                return;
        args.Cancelled = true;
    }
    private void OnAnyLimbSlotConditionValid(Entity<SurgeryAnyLimbSlotConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<SLBodyPartComponent>(args.Part, out var part))
            return;

        foreach (var socketId in part.Sockets.Keys)
        {
            if (IsInternalSocket(part, socketId) || _slBody.TryGetPartInSocket((args.Part, part), socketId, out _))
                continue;
            args.Suffix = SharedBodySystem.GetLegacySlotName(socketId);
            return;
        }

        args.Cancelled = true;
    }

    private void OnLimbSlotConditionValid(Entity<SurgeryLimbSlotConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var socketId = SharedBodySystem.GetSocketIdForLegacySlot(ent.Comp.Slot);
        args.Cancelled = !(TryComp<SLBodyPartComponent>(args.Part, out var part)
            && part.Sockets.ContainsKey(socketId)
            && !_slBody.TryGetPartInSocket((args.Part, part), socketId, out _));
    }

    private bool IsInternalSocket(SLBodyPartComponent part, string socketId)
        => part.Sockets.TryGetValue(socketId, out var socketType)
            && _prototypes.TryIndex(socketType, out var proto)
            && proto.Internal;
}
