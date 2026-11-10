// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Systems;

/// <summary>
/// Gibbing: organs (and whatever the body carried) fly out as giblets, the remaining parts are destroyed.
/// Keeping organs means the brain survives a gib and the character can be brought back.
/// </summary>
public sealed partial class SLBodySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly ProtoId<BodyPartTypePrototype> OrganPartType = "Organ";

    [SubscribeLocalEvent]
    private void OnBeingGibbed(Entity<SLBodyComponent> body, ref BeingGibbedEvent args)
    {
        var parts = new List<Entity<SLBodyPartComponent>>(GetBodyParts(body));
        args.Giblets.EnsureCapacity(args.Giblets.Count + parts.Count);
        foreach (var part in parts)
        {
            if (part.Comp.PartType == OrganPartType)
                args.Giblets.Add(part);
            else
                PredictedQueueDel(part.Owner);
        }

        foreach (var item in _inventory.GetHandOrInventoryEntities(body.Owner))
            args.Giblets.Add(item);
    }
}
