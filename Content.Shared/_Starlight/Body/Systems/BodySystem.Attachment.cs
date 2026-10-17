// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Systems;

/// <summary>
/// Attaching and detaching parts at runtime, and queries over the part tree.
/// </summary>
public sealed partial class SLBodySystem
{
    #region Attachment

    /// <summary>
    /// Attaches <paramref name="part"/> (with everything already attached to it) to a free socket of
    /// <paramref name="parent"/>. The parent does not have to belong to a body: a hand can be attached to a severed arm.
    /// </summary>
    public bool TryAttachPart(Entity<SLBodyPartComponent> parent, string socketId, Entity<SLBodyPartComponent> part)
    {
        if (part.Comp.Parent != null || part.Comp.Body != null || part.Owner == parent.Owner)
            return false;

        if (!CanAttach(parent, socketId, part, out var socket))
            return false;

        var children = EnsureChildren(parent);
        part.Comp.ParentSocket = socket;
        part.Comp.Parent = parent;
        if (!ContainerSystem.Insert(part.Owner, children))
        {
            part.Comp.ParentSocket = null;
            part.Comp.Parent = null;
            return false;
        }

        // Body membership and the entered events follow from the container insertion (OnChildInserted).
        Dirty(part);
        return true;
    }

    /// <summary>
    /// Checks that <paramref name="socketId"/> exists on <paramref name="parent"/>, is free and accepts the part type.
    /// </summary>
    public bool CanAttach(Entity<SLBodyPartComponent> parent, string socketId, Entity<SLBodyPartComponent> part,
        [NotNullWhen(true)] out BodyPartSocket? socket)
    {
        socket = null;
        if (!parent.Comp.Sockets.TryGetValue(socketId, out var socketTypeId)
            || !Proto.TryIndex(socketTypeId, out var socketType))
            return false;

        if (socketType.AllowedTypes != null && !socketType.AllowedTypes.Contains(part.Comp.PartType))
            return false;

        if (TryGetPartInSocket(parent, socketId, out _))
            return false;

        socket = new BodyPartSocket(socketId, socketTypeId);
        return true;
    }

    /// <summary>
    /// Detaches a part, together with everything attached to it, from its parent. The root part cannot be detached.
    /// </summary>
    /// <param name="destination">Where the detached part ends up; defaults to the parent's position.</param>
    public bool TryDetachPart(Entity<SLBodyPartComponent> part, EntityCoordinates? destination = null)
    {
        if (part.Comp.Parent is not { } parentUid
            || !ContainerSystem.TryGetContainer(parentUid, BodyContainerId, out var container)
            || !container.Contains(part))
            return false;

        // Leaving the body and clearing Parent/ParentSocket follow from the container removal (OnChildRemoved).
        return ContainerSystem.Remove(part.Owner, container, destination: destination);
    }

    private void RaiseEnteredBody(Entity<SLBodyComponent> body, Entity<SLBodyPartComponent> part)
    {
        var ev = new BodyPartEnteredBodyEvent(body, part);
        RaiseLocalEvent(part, ref ev);
        RaiseLocalEvent(body, ref ev);
    }

    #endregion

    #region Queries

    /// <summary>
    /// The part and everything attached to or contained in it, parents before children.
    /// </summary>
    public List<Entity<SLBodyPartComponent>> GetSubtree(Entity<SLBodyPartComponent> part)
    {
        var result = new List<Entity<SLBodyPartComponent>>();
        CollectSubtree(part, result);
        return result;
    }

    private void CollectSubtree(Entity<SLBodyPartComponent> part, List<Entity<SLBodyPartComponent>> result)
    {
        result.Add(part);
        foreach (var child in GetChildParts(part))
            CollectSubtree(child, result);
    }

    /// <summary>
    /// Parts directly attached to or contained in <paramref name="part"/>.
    /// </summary>
    public IEnumerable<Entity<SLBodyPartComponent>> GetChildParts(Entity<SLBodyPartComponent> part)
    {
        if (!ContainerSystem.TryGetContainer(part, BodyContainerId, out var children))
            yield break;

        foreach (var child in children.ContainedEntities)
        {
            if (TryComp<SLBodyPartComponent>(child, out var childComp))
                yield return (child, childComp);
        }
    }

    public bool TryGetPartInSocket(Entity<SLBodyPartComponent> parent, string socketId, out Entity<SLBodyPartComponent> part)
    {
        foreach (var child in GetChildParts(parent))
        {
            if (child.Comp.ParentSocket?.SocketId != socketId)
                continue;
            part = child;
            return true;
        }

        part = default;
        return false;
    }

    /// <summary>
    /// Whether the part sits in an internal socket (organs, implants).
    /// </summary>
    public bool IsInternal(Entity<SLBodyPartComponent> part)
        => part.Comp.ParentSocket is { } socket
           && Proto.TryIndex(socket.SocketType, out var socketType)
           && socketType.Internal;

    public IEnumerable<Entity<SLBodyPartComponent>> GetBodyPartsOfType(Entity<SLBodyComponent> body,
        ProtoId<BodyPartTypePrototype> partType)
    {
        foreach (var part in GetBodyParts(body))
        {
            if (part.Comp.PartType == partType)
                yield return part;
        }
    }

    public IEnumerable<Entity<SLBodyPartComponent, T>> GetBodyPartsWithComp<T>(Entity<SLBodyComponent> body)
        where T : IComponent
    {
        foreach (var part in GetBodyParts(body))
        {
            if (TryComp<T>(part, out var comp))
                yield return (part.Owner, part.Comp, comp);
        }
    }

    /// <summary>
    /// The socket path from the root part to this part, e.g. <c>/LeftArm/Hand</c>.
    /// </summary>
    public BodyPartAddress GetPartAddress(Entity<SLBodyPartComponent> part)
    {
        var sockets = new List<string>();
        var current = part;
        while (current.Comp.ParentSocket is { } socket)
        {
            sockets.Add(socket.SocketId);
            if (current.Comp.Parent is not { } parentUid || !TryComp<SLBodyPartComponent>(parentUid, out var parentComp))
                break;
            current = (parentUid, parentComp);
        }

        var address = BodyPartAddress.Root;
        for (var i = sockets.Count - 1; i >= 0; i--)
            address = address.Append(sockets[i]);
        return address;
    }

    /// <summary>
    /// Which side of the body the part is on, taken from the nearest socket on its path whose id starts with
    /// "Left" or "Right" (e.g. the hand in <c>/LeftArm/Hand</c> is on the left), unless the part itself names its
    /// <see cref="SLBodyPartComponent.Side"/>.
    /// </summary>
    public BodySide GetPartSide(Entity<SLBodyPartComponent> part)
    {
        if (part.Comp.Side != BodySide.None)
            return part.Comp.Side;

        var current = part;
        while (current.Comp.ParentSocket is { } socket)
        {
            if (socket.SocketId.StartsWith("Left", StringComparison.Ordinal))
                return BodySide.Left;
            if (socket.SocketId.StartsWith("Right", StringComparison.Ordinal))
                return BodySide.Right;
            if (current.Comp.Parent is not { } parentUid || !TryComp<SLBodyPartComponent>(parentUid, out var parentComp))
                break;
            current = (parentUid, parentComp);
        }

        return BodySide.None;
    }

    #endregion
}

public enum BodySide : byte
{
    None,
    Left,
    Right,
}
