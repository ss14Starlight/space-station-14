// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
namespace Content.Shared._Starlight.Body.System;

public sealed class BodySystem : EntitySystem
{
    public const string BodyContainerId = "sl_childbodyparts";
    [Dependency] private readonly SharedContainerSystem ContainerSystem = default!;
    [Dependency] private readonly IPrototypeManager Proto = default!;

    private event Action<BodySystem, Entity<SLBodyComponent>>? _initRelays = null;
    public override void Initialize()
    {
        SubscribeLocalEvent<SLBodyComponent, MapInitEvent>(OnBodyMapInit);
        SubscribeLocalEvent<SLBodyComponent, BodyInitEvent>(OnBodyInit);
    }

    private void OnBodyInit(Entity<SLBodyComponent> body, ref BodyInitEvent args)
    {
        foreach (var bodyPart in body.Comp.BodyParts)
            RaiseLocalEvent(bodyPart, new BodyPartInitEvent(body) );
        _initRelays?.Invoke(this, body);
    }
    public void SubscribeBodyPartInitEvent<TBodyComp>(EntityEventHandler<BodyPartInitEvent<TBodyComp>> handler) where TBodyComp: IComponent
    {
        _initRelays += (system, body) =>
        {
            var comp = system.Comp<TBodyComp>(body);
            var ev = new BodyPartInitEvent<TBodyComp>(body, comp);
            foreach (var part in body.Comp.BodyParts)
                system.RaiseLocalEvent(part, ev);
        };
    }

    private void OnBodyMapInit(Entity<SLBodyComponent> body, ref MapInitEvent args)
    {
        if (body.Comp.BodyBuilt) //We don't want to initialize body twice!
            return;
        body.Comp.BodyBuilt = BuildBody(body);
        if (body.Comp.BodyBuilt)
            Dirty(body);
    }

    public bool BuildBody(Entity<SLBodyComponent> body)
    {
        if (body.Comp.BodyBuilt ||  !Proto.TryIndex(body.Comp.Prefab, out var prefabProto))
            return false; //body has already been built, or no valid prefab :(
        MakeBodyParts(body, prefabProto);
        body.Comp.BodyBuilt = true;
        Dirty(body);
        RaiseLocalEvent(body, new BodyInitEvent());
        return true;
    }

    private Entity<SLBodyPartComponent> MakeBodyParts(Entity<SLBodyComponent> body, BodyPrefabPrototype prefab)
    {
        body.Comp.RootPartEntity = EntityManager.SpawnAttachedTo(prefab.Root.BodyPart, new(body.Owner, body.Comp.RootOffset));
        var bodyPart = new Entity<SLBodyPartComponent>(body.Comp.RootPartEntity,
            Comp<SLBodyPartComponent>(body.Comp.RootPartEntity));
        RecursivelyBuildBodyParts(body, null, bodyPart, prefab.Root);
        return bodyPart;
    }

    private void RecursivelyBuildBodyParts(Entity<SLBodyComponent> body, BodyPartContainer? parentContainer,Entity<SLBodyPartComponent> newPart,
        BodyPartDef partDef)
    {
        var children = EnsureChildren(newPart); //Create the root BodyPart container and ensure that sockets are properly setup!
        body.Comp.BodyParts.Add(newPart); //cache the new bodypart
        newPart.Comp.Body = body;
        if (partDef.SocketedParts != null)
            foreach (var (socket, def) in partDef.SocketedParts)
            {
                var newChild = EntityManager.SpawnAttachedTo(def.BodyPart, new EntityCoordinates(newPart, 0, 0));
                var childPart = new Entity<SLBodyPartComponent>(newChild, Comp<SLBodyPartComponent>(newChild));
                childPart.Comp.ParentSocket = socket;
                childPart.Comp.Parent = newPart;
                RecursivelyBuildBodyParts(body, children, childPart, partDef);
            }

        if (partDef.InternalParts != null)
            foreach (var def in partDef.InternalParts)
            {
                var newChild = EntityManager.SpawnAttachedTo(def.BodyPart, new EntityCoordinates(newPart, 0, 0));
                var childPart = new Entity<SLBodyPartComponent>(newChild, Comp<SLBodyPartComponent>(newChild));
                childPart.Comp.Parent = newPart;
                RecursivelyBuildBodyParts(body, children, childPart, partDef);
            }
        Dirty(newPart);
        if (parentContainer != null)
        {
            //Make sure to insert the body part LAST
            ContainerSystem.Insert(newPart.Owner, parentContainer, force: true);
        }
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
        foreach (var (id, socketTypeId) in bodyPart.Comp.Sockets)
        {
            var socketType = Proto.Index(socketTypeId);
            container.TryRegisterSocket(new BodyPartSocket(id, socketType.ID));
        }
    }
}
