// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
namespace Content.Shared._Starlight.Body.Systems;

public sealed partial class SLBodySystem : EntitySystem
{
    public const string BodyContainerId = "sl_childbodyparts";

    /// <summary>
    /// Container on the body entity that holds the root part, so it is hidden and moves with the body.
    /// </summary>
    public const string RootContainerId = "sl_rootbodypart";
    [Dependency] private SharedContainerSystem ContainerSystem = default!;
    [Dependency] private IPrototypeManager Proto = default!;
    [Dependency] private BodyPartVisualizerSystem BodyPartVisualizer = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SLBodyComponent, MapInitEvent>(OnBodyMapInit);
        SubscribeLocalEvent<SLBodyComponent, AfterAutoHandleStateEvent>(OnBodyHandleState);
        SubscribeLocalEvent<SLBodyPartComponent, AfterAutoHandleStateEvent>(OnPartHandleState);
        SubscribeLocalEvent<SLBodyPartComponent, SLBodyPartAddedEvent>(OnPartAdded);
        SubscribeLocalEvent<SLBodyPartComponent, SLBodyPartRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<SLBodyPartComponent, EntInsertedIntoContainerMessage>(OnChildInserted);
        SubscribeLocalEvent<SLBodyPartComponent, EntRemovedFromContainerMessage>(OnChildRemoved);
        InitializeGib();
    }

    // Child parts live in a plain Container (the client can only rebuild Container/ContainerSlot). Its messages drive
    // body membership, so a part entering or leaving the container by any means (attach/detach, deletion, a
    // transform change) updates Body and raises the entered/left events. On the client, state application only
    // syncs the networked fields; the events come from the server's changes.
    private void OnChildInserted(Entity<SLBodyPartComponent> parent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BodyContainerId || !TryComp<SLBodyPartComponent>(args.Entity, out var childComp))
            return;
        Entity<SLBodyPartComponent> child = (args.Entity, childComp);
        var added = new SLBodyPartAddedEvent(child);
        RaiseLocalEvent(child, ref added);

        if (_timing.ApplyingState)
            return;

        child.Comp.Parent ??= parent;
        // Built parts already carry the body; BuildBody raises their entered events once the tree is complete.
        if (parent.Comp.Body is not { } bodyUid || child.Comp.Body == bodyUid
            || !TryComp<SLBodyComponent>(bodyUid, out var bodyComp))
            return;

        Entity<SLBodyComponent> body = (bodyUid, bodyComp);
        body.Comp.BodyPartsDirty = true;
        foreach (var entered in GetSubtree(child))
        {
            entered.Comp.Body = body;
            Dirty(entered);
            RaiseEnteredBody(body, entered);
        }
    }

    private void OnChildRemoved(Entity<SLBodyPartComponent> parent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != BodyContainerId || !TryComp<SLBodyPartComponent>(args.Entity, out var childComp))
            return;
        Entity<SLBodyPartComponent> child = (args.Entity, childComp);
        var removed = new SLBodyPartRemovedEvent(child);
        RaiseLocalEvent(child, ref removed);

        if (_timing.ApplyingState)
            return;

        // Left-body handlers still see the old Parent/ParentSocket; they are cleared afterwards.
        if (child.Comp.Body is { } bodyUid && TryComp<SLBodyComponent>(bodyUid, out var bodyComp))
        {
            Entity<SLBodyComponent> body = (bodyUid, bodyComp);
            body.Comp.BodyPartsDirty = true;
            if (!TerminatingOrDeleted(bodyUid))
            {
                var subtree = GetSubtree(child);
                for (var i = subtree.Count - 1; i >= 0; i--)
                {
                    var left = subtree[i];
                    left.Comp.Body = null;
                    Dirty(left);
                    var ev = new BodyPartLeftBodyEvent(body, left);
                    RaiseLocalEvent(left, ref ev);
                    RaiseLocalEvent(body, ref ev);
                }
            }
        }

        child.Comp.ParentSocket = null;
        child.Comp.Parent = null;
        Dirty(child);
    }

    /// <summary>
    /// All parts of the body, root first, parents before children. Rebuilt from the part tree on demand,
    /// so it is valid on the client as well. Do not attach or detach parts while iterating it.
    /// </summary>
    public IReadOnlyList<Entity<SLBodyPartComponent>> GetBodyParts(Entity<SLBodyComponent> body)
    {
        if (body.Comp.BodyPartsDirty)
            RebuildBodyPartCache(body);
        return body.Comp.BodyParts;
    }

    private void RebuildBodyPartCache(Entity<SLBodyComponent> body)
    {
        body.Comp.BodyParts.Clear();
        body.Comp.BodyPartsDirty = false;
        if (body.Comp.RootPartEntity is { } rootUid && TryComp<SLBodyPartComponent>(rootUid, out var root))
            CollectBodyParts(body, (rootUid, root));
    }

    private void CollectBodyParts(Entity<SLBodyComponent> body, Entity<SLBodyPartComponent> part)
    {
        body.Comp.BodyParts.Add(part);
        if (!ContainerSystem.TryGetContainer(part, BodyContainerId, out var children))
            return;
        foreach (var child in children.ContainedEntities)
        {
            if (TryComp<SLBodyPartComponent>(child, out var childComp))
                CollectBodyParts(body, (child, childComp));
        }
    }

    private void MarkBodyPartsDirty(EntityUid? body)
    {
        if (TryComp<SLBodyComponent>(body, out var bodyComp))
            bodyComp.BodyPartsDirty = true;
    }

    private void OnBodyHandleState(Entity<SLBodyComponent> body, ref AfterAutoHandleStateEvent args)
        => body.Comp.BodyPartsDirty = true;

    private void OnPartHandleState(Entity<SLBodyPartComponent> part, ref AfterAutoHandleStateEvent args)
        => MarkBodyPartsDirty(part.Comp.Body);

    private void OnPartAdded(Entity<SLBodyPartComponent> part, ref SLBodyPartAddedEvent args)
        => MarkBodyPartsDirty(part.Comp.Body);

    private void OnPartRemoved(Entity<SLBodyPartComponent> part, ref SLBodyPartRemovedEvent args)
        => MarkBodyPartsDirty(part.Comp.Body);
    private void OnBodyMapInit(Entity<SLBodyComponent> body, ref MapInitEvent args)
    {
        if (body.Comp.BodyBuilt) //We don't want to initialize body twice!
            return;
        if (BuildBody(body))
            Dirty(body);
    }

    public bool BuildBody(Entity<SLBodyComponent> body)
    {
        if (body.Comp.BodyBuilt ||  !Proto.TryIndex(body.Comp.Prefab, out var prefabProto))
            return false; //body has already been built, or no valid prefab :(
        MakeBodyParts(body, prefabProto);
        body.Comp.BodyBuilt = true;
        body.Comp.BodyPartsDirty = true;
        Dirty(body);
        foreach (var part in GetBodyParts(body))
            RaiseEnteredBody(body, part);
        RaiseLocalEvent(body, new BodyInitEvent());
        return true;
    }

    private Entity<SLBodyPartComponent> MakeBodyParts(Entity<SLBodyComponent> body, BodyPrefabPrototype prefab)
    {
        var rootUid = EntityManager.SpawnAttachedTo(prefab.Root.BodyPart, new(body.Owner, body.Comp.RootOffset));
        ContainerSystem.Insert(rootUid, ContainerSystem.EnsureContainer<ContainerSlot>(body.Owner, RootContainerId), force: true);
        body.Comp.RootPartEntity = rootUid;
        var bodyPart = new Entity<SLBodyPartComponent>(rootUid, Comp<SLBodyPartComponent>(rootUid));
        RecursivelyBuildBodyParts(body, null, bodyPart, prefab.Root, BodyPartAddress.Root);
        Dirty(body);
        Dirty(bodyPart);
        return bodyPart;
    }

    private void RecursivelyBuildBodyParts(Entity<SLBodyComponent> body, Container? parentContainer, Entity<SLBodyPartComponent> newPart,
        BodyPartDef partDef,
        BodyPartAddress address)
    {
        var children = EnsureChildren(newPart);
        newPart.Comp.Body = body;
        if (partDef.Sockets != null)
        {
            foreach (var (socketId, socketType) in partDef.Sockets)
                AddSocket(newPart, socketId, socketType);
        }
        if (partDef.AttachedParts != null)
            BuildChildParts(body, children, newPart, partDef.AttachedParts, address, internalSockets: false);
        if (partDef.ContainedParts != null)
            BuildChildParts(body, children, newPart, partDef.ContainedParts, address, internalSockets: true);
        BodyPartVisualizer.BakeAppearance(body, newPart, address);
        Dirty(newPart);
        if (parentContainer != null)
        {
            //Make sure to insert the body part LAST
            ContainerSystem.Insert(newPart.Owner, parentContainer, force: true);
        }
        else
        {
            // The root part sits in the body's root container, not in a parent's children; raise the event manually
            var ev = new SLBodyPartAddedEvent(newPart);
            RaiseLocalEvent(newPart, ref ev);
        }
    }

    private void BuildChildParts(Entity<SLBodyComponent> body,
        Container children,
        Entity<SLBodyPartComponent> parent,
        Dictionary<string, BodyPartDef> defs,
        BodyPartAddress address,
        bool internalSockets)
    {
        foreach (var (socketId, def) in defs)
        {
            if (!parent.Comp.Sockets.TryGetValue(socketId, out var socketType))
            {
                // The prefab defines the body's shape; sockets the part prototype does not declare are created.
                socketType = internalSockets
                    ? BodyPrefabPrototype.DefaultInternalSocket
                    : BodyPrefabPrototype.DefaultExternalSocket;
                AddSocket(parent, socketId, socketType);
            }

            if (Proto.Index(socketType).Internal != internalSockets)
            {
                Log.Error($"Body {ToPrettyString(body)}: socket '{socketId}' on {ToPrettyString(parent)} is " +
                        $"{(internalSockets ? "external" : "internal")}, list {def.BodyPart} under " +
                        $"{(internalSockets ? "attachedParts" : "containedParts")}");
                continue;
            }

            var newChild = EntityManager.SpawnAttachedTo(def.BodyPart, new EntityCoordinates(parent, 0, 0));
            var childPart = new Entity<SLBodyPartComponent>(newChild, Comp<SLBodyPartComponent>(newChild));
            childPart.Comp.ParentSocket = new BodyPartSocket(socketId, socketType);
            childPart.Comp.Parent = parent;
            RecursivelyBuildBodyParts(body, children, childPart, def, address.Append(socketId));
        }
    }

    /// <summary>
    /// Adds a socket to a part (or retypes an existing one).
    /// </summary>
    public void AddSocket(Entity<SLBodyPartComponent> part, string socketId, ProtoId<BodyPartSocketPrototype> socketType)
    {
        part.Comp.Sockets[socketId] = socketType;
        Dirty(part);
    }

    private Container EnsureChildren(Entity<SLBodyPartComponent> bodyPart)
    {
        return bodyPart.Comp.Children ??= ContainerSystem.EnsureContainer<Container>(bodyPart.Owner, BodyContainerId);
    }
}
