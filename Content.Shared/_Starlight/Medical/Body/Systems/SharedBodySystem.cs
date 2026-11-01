// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Organ;
using Content.Shared.DragDrop;
using Robust.Shared.Containers;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Systems;

/// <summary>
/// Organ and part queries over the body (<see cref="SLBodyComponent"/>) for the medical systems: organs are the
/// body parts that have an <see cref="OrganComponent"/>, everything else is a limb, a torso, a head and so on.
/// Also keeps <see cref="OrganComponent.Body"/> and the organ events in sync with the part tree.
/// </summary>
public abstract partial class SharedBodySystem : EntitySystem
{
    /// <summary>
    /// Container ID prefix of item containers on body parts, the cavity (<c>body_organ_slot_cavity</c>).
    /// </summary>
    public const string OrganSlotContainerIdPrefix = "body_organ_slot_";

    [Dependency] protected SharedContainerSystem Containers = default!;
    [Dependency] private SLBodySystem _slBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, BodyPartEnteredBodyEvent>(OnOrganEnteredBody);
        SubscribeLocalEvent<OrganComponent, BodyPartLeftBodyEvent>(OnOrganLeftBody);
        SubscribeLocalEvent<SLBodyComponent, CanDragEvent>(OnBodyCanDrag);
    }

    private void OnOrganEnteredBody(Entity<OrganComponent> organ, ref BodyPartEnteredBodyEvent args)
    {
        var parent = args.Part.Comp.Parent ?? EntityUid.Invalid;
        organ.Comp.Body = args.Body;
        Dirty(organ);

        var added = new OrganAddedEvent(parent);
        RaiseLocalEvent(organ, ref added);
        var addedToBody = new OrganAddedToBodyEvent(args.Body, parent);
        RaiseLocalEvent(organ, ref addedToBody);

        // Functional organs, marking organs and so on apply themselves on implantation.
        var implanted = new SurgeryOrganImplantationCompleted(args.Body, parent, organ);
        RaiseLocalEvent(organ, ref implanted);
    }

    private void OnOrganLeftBody(Entity<OrganComponent> organ, ref BodyPartLeftBodyEvent args)
    {
        var parent = args.Part.Comp.Parent ?? EntityUid.Invalid;
        var removed = new OrganRemovedEvent(parent);
        RaiseLocalEvent(organ, ref removed);
        var removedFromBody = new OrganRemovedFromBodyEvent(args.Body, parent);
        RaiseLocalEvent(organ, ref removedFromBody);

        organ.Comp.Body = null;
        Dirty(organ);
    }

    private void OnBodyCanDrag(Entity<SLBodyComponent> ent, ref CanDragEvent args)
        => args.Handled = true;

    #region Organs

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetBodyOrgans(EntityUid? bodyId, SLBodyComponent? body = null)
    {
        if (bodyId is not { } id || !Resolve(id, ref body, logMissing: false))
            yield break;

        foreach (var organ in _slBody.GetBodyPartsWithComp<OrganComponent>((id, body)))
            yield return (organ.Owner, organ.Comp2);
    }

    /// <summary>
    /// Every organ of the body that has <typeparamref name="T"/>.
    /// </summary>
    public List<Entity<T, OrganComponent>> GetBodyOrganEntityComps<T>(Entity<SLBodyComponent?> entity)
        where T : IComponent
    {
        var query = GetEntityQuery<T>();
        var list = new List<Entity<T, OrganComponent>>(3);
        foreach (var organ in GetBodyOrgans(entity.Owner, entity.Comp))
        {
            if (query.TryGetComponent(organ.Id, out var comp))
                list.Add((organ.Id, comp, organ.Component));
        }

        return list;
    }

    public bool TryGetOrgansWithComponent<T>(
        Entity<SLBodyComponent?> entity,
        [NotNullWhen(true)] out List<Entity<T, OrganComponent>>? comps)
        where T : IComponent
    {
        comps = GetBodyOrganEntityComps<T>(entity);
        if (comps.Count != 0)
            return true;

        comps = null;
        return false;
    }

    /// <summary>
    /// Organs held directly by the part.
    /// </summary>
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, SLBodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        foreach (var child in _slBody.GetChildParts((partId, part)))
        {
            if (TryComp<OrganComponent>(child, out var organ))
                yield return (child.Owner, organ);
        }
    }

    /// <summary>
    /// Whether some part of the body has a socket for the organ slot (<c>eyes</c> -> <c>Eyes</c>). Does not check
    /// that it holds an organ.
    /// </summary>
    public bool HasOrganSlot(EntityUid bodyId, string slotId)
    {
        if (!TryComp<SLBodyComponent>(bodyId, out var body))
            return false;

        var socketId = GetSocketIdForLegacySlot(slotId);
        foreach (var part in _slBody.GetBodyParts((bodyId, body)))
        {
            if (part.Comp.Sockets.ContainsKey(socketId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Takes the organ out of its body part, or out of the cavity it is stored in.
    /// </summary>
    public bool RemoveOrgan(EntityUid organId, OrganComponent? organ = null)
    {
        if (TryComp<SLBodyPartComponent>(organId, out var part) && part.Parent != null)
            return _slBody.TryDetachPart((organId, part));

        if (!Containers.TryGetContainingContainer((organId, null, null), out var container))
            return false;

        var containerId = container.ID;
        return containerId.StartsWith(OrganSlotContainerIdPrefix, StringComparison.Ordinal)
            && Containers.Remove(organId, container);
    }

    #endregion

    #region Parts

    /// <summary>
    /// The limbs, torso, head and other non-organ parts of the body, the root part first.
    /// </summary>
    public IEnumerable<(EntityUid Id, SLBodyPartComponent Component)> GetBodyChildren(EntityUid? bodyId, SLBodyComponent? body = null)
    {
        if (bodyId is not { } id || !Resolve(id, ref body, logMissing: false))
            yield break;

        foreach (var part in _slBody.GetBodyParts((id, body)))
        {
            if (!HasComp<OrganComponent>(part))
                yield return (part.Owner, part.Comp);
        }
    }

    public IEnumerable<(EntityUid Id, SLBodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid bodyId,
        BodyPartType type,
        SLBodyComponent? body = null)
    {
        foreach (var part in GetBodyChildren(bodyId, body))
        {
            if (part.Component.Kind == type)
                yield return part;
        }
    }

    /// <summary>
    /// The part this part or organ is attached to.
    /// </summary>
    public EntityUid? GetParentPartOrNull(EntityUid uid)
        => TryComp<SLBodyPartComponent>(uid, out var part) ? part.Parent : null;

    public bool TryGetParentBodyPart(
        EntityUid partUid,
        [NotNullWhen(true)] out EntityUid? parentUid,
        [NotNullWhen(true)] out SLBodyPartComponent? parentComponent)
    {
        parentComponent = null;
        parentUid = GetParentPartOrNull(partUid);
        return parentUid != null && TryComp(parentUid, out parentComponent);
    }

    #endregion

    /// <summary>
    /// The container ID of an item slot on a body part, the cavity.
    /// </summary>
    public static string GetOrganContainerId(string slotId)
        => OrganSlotContainerIdPrefix + slotId;

    /// <summary>
    /// Socket id for a slot name: <c>left arm</c> / <c>left_arm</c> -> <c>LeftArm</c>.
    /// </summary>
    public static string GetSocketIdForLegacySlot(string slotId)
    {
        var builder = new StringBuilder(slotId.Length);
        var upper = true;
        foreach (var c in slotId)
        {
            if (c is ' ' or '_' or '-')
            {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Slot name for a socket id: <c>LeftArm</c> -> <c>left arm</c>, the inverse of <see cref="GetSocketIdForLegacySlot"/>.
    /// </summary>
    public static string GetLegacySlotName(string socketId)
    {
        var builder = new StringBuilder(socketId.Length + 2);
        for (var i = 0; i < socketId.Length; i++)
        {
            var c = socketId[i];
            if (char.IsUpper(c) && i > 0)
                builder.Append(' ');
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
