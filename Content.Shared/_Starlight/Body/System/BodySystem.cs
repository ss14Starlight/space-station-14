// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.System;

public sealed class BodySystem : EntitySystem
{
    public const string BodyContainerId = "sl_childbodyparts";
    [Dependency] private readonly SharedContainerSystem ContainerSystem = default!;
    [Dependency] private readonly IPrototypeManager Proto = default!;

    public override void Initialize()
    {
    }

    private BodyPartContainer EnsureChildren(Entity<SLBodyPartComponent> bodyPart)
    {
        if (bodyPart.Comp.Children != null)
            return bodyPart.Comp.Children;
        var exists = false;
        bodyPart.Comp.Children ??= ContainerSystem.EnsureContainer<BodyPartContainer>(bodyPart.Owner, BodyContainerId, out exists);
        if (!exists) //We created a new child container and need to populate its sockets
            PopulateSockets(bodyPart);
        return bodyPart.Comp.Children;
    }

    private void PopulateSockets(Entity<SLBodyPartComponent> bodyPart)
    {
        if (bodyPart.Comp.Children == null)
            EnsureChildren(bodyPart);
        var container = bodyPart.Comp.Children!;
        foreach (var (id, socketTypeId) in bodyPart.Comp.PartSockets)
        {
            var socketType = Proto.Index(socketTypeId);
            container.TryRegisterSocket(new BodyPartSocket(id, socketType.ID, socketType.AllowedTypes));
        }
    }
}
