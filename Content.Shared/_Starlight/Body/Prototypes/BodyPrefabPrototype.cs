// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT
using Content.Shared._Starlight.Body.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Prototypes;

[Prototype]
public sealed partial class BodyPrefabPrototype  : IPrototype
{
    [IdDataField] public string ID { get; set; } = string.Empty;

    [DataField(required: true)] public BodyPartDef Root = default!;

    /// <summary>
    /// Finds the part the prefab puts into a socket with this id (first match, depth-first), e.g. "Brain".
    /// </summary>
    public bool TryFindPart(string socketId, out EntProtoId<SLBodyPartComponent> part)
        => TryFindPart(Root, socketId, out part);

    private static bool TryFindPart(BodyPartDef def, string socketId, out EntProtoId<SLBodyPartComponent> part)
    {
        foreach (var children in new[] { def.ContainedParts, def.AttachedParts })
        {
            if (children == null)
                continue;
            foreach (var (socket, child) in children)
            {
                if (socket == socketId)
                {
                    part = child.BodyPart;
                    return true;
                }

                if (TryFindPart(child, socketId, out part))
                    return true;
            }
        }

        part = default;
        return false;
    }

    public static readonly ProtoId<BodyPartSocketPrototype> DefaultExternalSocket = "PartSocket";
    public static readonly ProtoId<BodyPartSocketPrototype> DefaultInternalSocket = "OrganSocket";
}

/// <param name="AttachedParts">Parts attached to this part's external sockets, keyed by socket id.</param>
/// <param name="ContainedParts">Parts held in this part's internal sockets (organs, implants), keyed by socket id.</param>
/// <param name="Sockets">Extra sockets this part gets in this body, e.g. empty implant slots.</param>
/// <remarks>
/// The prefab is the source of truth for the body's shape: a socket used by <paramref name="AttachedParts"/> or
/// <paramref name="ContainedParts"/> that the part prototype does not declare is created with
/// <see cref="BodyPrefabPrototype.DefaultExternalSocket"/> / <see cref="BodyPrefabPrototype.DefaultInternalSocket"/>.
/// </remarks>
[DataRecord]
public partial record struct BodyPartDef(
    EntProtoId<SLBodyPartComponent> BodyPart,
    Dictionary<string, BodyPartDef>? AttachedParts = null,
    Dictionary<string, BodyPartDef>? ContainedParts = null,
    List<EntProtoId<SLBodyPartComponent>>? Alternatives = null,
    Dictionary<string, ProtoId<BodyPartSocketPrototype>>? Sockets = null)
{
    public bool HasSocketedChildren => AttachedParts is { Count: > 0 };
    public bool HasInternalChildren => ContainedParts is { Count: > 0 };
    public bool HasAlternatives => Alternatives is { Count: > 0 };
};
