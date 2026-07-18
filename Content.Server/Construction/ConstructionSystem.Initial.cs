using System.IO;
using System.Linq;
using Content.Server.Construction.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Construction
{
    public sealed partial class ConstructionSystem
    {
        [Dependency] private InventorySystem _inventorySystem = default!;
        [Dependency] private SharedInteractionSystem _interactionSystem = default!;
        [Dependency] private ActionBlockerSystem _actionBlocker = default!;
        [Dependency] private SharedHandsSystem _handsSystem = default!;
        [Dependency] private EntityLookupSystem _lookupSystem = default!;
        [Dependency] private SharedTransformSystem _transformSystem = default!;
        [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
        [Dependency] private ISharedPlayerManager _playerManager = default!;

        // --- WARNING! LEGACY CODE AHEAD! ---
        // This entire file contains the legacy code for initial construction.
        // This is bound to be replaced by a better alternative (probably using dummy entities)
        // but for now I've isolated them in their own little file. This code is largely unchanged.
        // --- YOU HAVE BEEN WARNED! AAAH! ---

        private readonly Dictionary<ICommonSession, HashSet<int>> _beingBuilt = new();

        private void InitializeInitial()
        {
            SubscribeNetworkEvent<TryStartStructureConstructionMessage>(HandleStartStructureConstruction);
            SubscribeNetworkEvent<TryStartItemConstructionMessage>(HandleStartItemConstruction);
            SubscribeLocalEvent<PendingInitialConstructionComponent, InitialConstructionDoAfterEvent>(OnInitialConstructionDoAfter);
            SubscribeLocalEvent<PendingInitialConstructionComponent, EntityTerminatingEvent>(OnPendingInitialConstructionTerminating);
        }

        // LEGACY CODE. See warning at the top of the file!
        private IEnumerable<EntityUid> EnumerateNearby(EntityUid user)
        {
            foreach (var item in _handsSystem.EnumerateHeld(user))
            {
                if (TryComp(item, out StorageComponent? storage))
                {
                    foreach (var storedEntity in storage.Container.ContainedEntities!)
                    {
                        yield return storedEntity;
                    }
                }

                yield return item;
            }

            if (_inventorySystem.TryGetContainerSlotEnumerator(user, out var containerSlotEnumerator))
            {
                while (containerSlotEnumerator.MoveNext(out var containerSlot))
                {
                    if(!containerSlot.ContainedEntity.HasValue)
                        continue;

                    if (TryComp(containerSlot.ContainedEntity.Value, out StorageComponent? storage))
                    {
                        foreach (var storedEntity in storage.Container.ContainedEntities)
                        {
                            yield return storedEntity;
                        }
                    }

                    yield return containerSlot.ContainedEntity.Value;
                }
            }

            var pos = _transformSystem.GetMapCoordinates(user);

            foreach (var near in _lookupSystem.GetEntitiesInRange(pos, 2f, LookupFlags.Contained | LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate))
            {
                if (near == user)
                    continue;
                if (_interactionSystem.InRangeUnobstructed(pos, near, 2f) && _container.IsInSameOrParentContainer(user, near))
                    yield return near;
            }
        }

        /// <summary>
        /// Reserves construction materials on the user and starts a DoAfter. Completion is handled by
        /// <see cref="OnInitialConstructionDoAfter"/>.
        /// </summary>
        /// <returns>True if materials were reserved and the DoAfter was started.</returns>
        private bool TryBeginConstruct(
            EntityUid user,
            string materialContainer,
            ConstructionGraphPrototype graph,
            ConstructionGraphEdge edge,
            ConstructionGraphNode targetNode,
            EntityCoordinates coords,
            Angle angle,
            InitialConstructionKind kind,
            ProtoId<ConstructionPrototype> constructionId,
            int? structureAck = null,
            NetUserId? sessionUserId = null)
        {
            // We need a place to hold our construction items!
            var container = _container.EnsureContainer<Container>(user, materialContainer, out var existed);

            if (existed)
            {
                _popup.PopupEntity(Loc.GetString("construction-system-construct-cannot-start-another-construction"), user, user);
                return false;
            }

            var containers = new Dictionary<string, Container>();
            var storeContainerIds = new Dictionary<string, string>();
            var doAfterTime = 0f;

            // HOLY SHIT THIS IS SOME HACKY CODE.
            // But I'd rather do this shit than risk having collisions with other containers.
            Container GetContainer(string name)
            {
                if (containers.TryGetValue(name, out var container1))
                    return container1;

                while (true)
                {
                    var random = _robustRandom.Next();
                    var id = random.ToString();
                    var c = _container.EnsureContainer<Container>(user, id, out var exists);

                    if (exists)
                        continue;

                    containers[name] = c;
                    storeContainerIds[name] = id;
                    return c;
                }
            }

            void FailCleanup()
            {
                RestoreReservedMaterials(user, materialContainer, storeContainerIds);
            }

            var failed = false;
            var used = new HashSet<EntityUid>();

            foreach (var step in edge.Steps)
            {
                doAfterTime += step.DoAfter;

                var handled = false;

                switch (step)
                {
                    case MaterialConstructionGraphStep materialStep:
                        foreach (var entity in EnumerateNearby(user))
                        {
                            if (!materialStep.EntityValid(entity, out var stack))
                                continue;

                            if (used.Contains(entity))
                                continue;

                            // TODO allow taking from several stacks.
                            // Also update crafting steps to check if it works.
                            var splitStack = _stackSystem.Split((entity, stack), materialStep.Amount, user.ToCoordinates(0, 0));

                            if (splitStack == null)
                                continue;

                            if (string.IsNullOrEmpty(materialStep.Store))
                            {
                                if (!_container.Insert(splitStack.Value, container))
                                    continue;
                            }
                            else if (!_container.Insert(splitStack.Value, GetContainer(materialStep.Store)))
                                continue;

                            handled = true;
                            break;
                        }

                        break;

                    case ArbitraryInsertConstructionGraphStep arbitraryStep:
                        foreach (var entity in new HashSet<EntityUid>(EnumerateNearby(user)))
                        {
                            if (!arbitraryStep.EntityValid(entity, EntityManager, Factory))
                                continue;

                            if (used.Contains(entity))
                                continue;

                            // Dump out any stored entities in used entity
                            if (TryComp<StorageComponent>(entity, out var storage))
                            {
                                _container.EmptyContainer(storage.Container);
                            }

                            if (string.IsNullOrEmpty(arbitraryStep.Store))
                            {
                                if (!_container.Insert(entity, container))
                                    continue;
                            }
                            else if (!_container.Insert(entity, GetContainer(arbitraryStep.Store)))
                                continue;

                            handled = true;
                            used.Add(entity);
                            break;
                        }

                        break;
                }

                if (handled == false)
                {
                    failed = true;
                    break;
                }
            }

            if (failed)
            {
                _popup.PopupEntity(Loc.GetString("construction-system-construct-no-materials"), user, user);
                FailCleanup();
                return false;
            }

            var pending = EnsureComp<PendingInitialConstructionComponent>(user);
            var operationId = pending.NextOperationId++;
            pending.Operations[operationId] = new PendingInitialConstruction
            {
                Kind = kind,
                ConstructionId = constructionId,
                GraphId = graph.ID,
                EdgeTarget = edge.Target,
                TargetNode = targetNode.Name,
                PrimaryContainerId = materialContainer,
                StoreContainers = storeContainerIds,
                Coordinates = coords,
                Angle = angle,
                StructureAck = structureAck,
                SessionUserId = sessionUserId,
            };

            var doAfterArgs = new DoAfterArgs(EntityManager, user, doAfterTime, new InitialConstructionDoAfterEvent(operationId), user)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = false,
                // allow simultaneously starting several construction jobs using the same stack of materials.
                CancelDuplicate = false,
                BlockDuplicate = false,
            };

            if (!_doAfterSystem.TryStartDoAfter(doAfterArgs))
            {
                pending.Operations.Remove(operationId);
                FailCleanup();
                return false;
            }

            return true;
        }

        private void OnInitialConstructionDoAfter(Entity<PendingInitialConstructionComponent> ent, ref InitialConstructionDoAfterEvent args)
        {
            if (args.Handled)
                return;

            if (!ent.Comp.Operations.Remove(args.OperationId, out var pending))
                return;

            args.Handled = true;

            if (args.Cancelled)
            {
                CancelPendingConstruction(ent.Owner, pending);
                return;
            }

            CompletePendingConstruction(ent.Owner, pending);
        }

        private void OnPendingInitialConstructionTerminating(Entity<PendingInitialConstructionComponent> ent, ref EntityTerminatingEvent args)
        {
            foreach (var pending in ent.Comp.Operations.Values.ToArray())
            {
                CancelPendingConstruction(ent.Owner, pending, restoreMaterials: false);
            }

            ent.Comp.Operations.Clear();
        }

        private void CancelPendingConstruction(EntityUid user, PendingInitialConstruction pending, bool restoreMaterials = true)
        {
            if (restoreMaterials && Exists(user))
                RestoreReservedMaterials(user, pending.PrimaryContainerId, pending.StoreContainers);

            ReleaseStructureAck(pending);
        }

        private void CompletePendingConstruction(EntityUid user, PendingInitialConstruction pending)
        {
            try
            {
                if (!Exists(user))
                {
                    CancelPendingConstruction(user, pending, restoreMaterials: false);
                    return;
                }

                if (!PrototypeManager.TryIndex(pending.GraphId, out ConstructionGraphPrototype? graph)
                    || !graph.Nodes.TryGetValue(pending.EdgeTarget, out var edgeNode)
                    || !graph.Nodes.TryGetValue(pending.TargetNode, out var targetNode))
                {
                    CancelPendingConstruction(user, pending);
                    return;
                }

                // Find the edge that targets EdgeTarget from the construction start. Re-index via construction prototype.
                if (!PrototypeManager.TryIndex(pending.ConstructionId, out ConstructionPrototype? constructionPrototype)
                    || !graph.Nodes.TryGetValue(constructionPrototype.StartNode, out var startNode))
                {
                    CancelPendingConstruction(user, pending);
                    return;
                }

                var pathFind = graph.Path(startNode.Name, targetNode.Name);
                if (pathFind == null || pathFind.Length == 0)
                {
                    CancelPendingConstruction(user, pending);
                    return;
                }

                var edge = startNode.GetEdge(pathFind[0].Name);
                if (edge == null || edge.Target != pending.EdgeTarget)
                {
                    CancelPendingConstruction(user, pending);
                    return;
                }

                if (!_container.TryGetContainer(user, pending.PrimaryContainerId, out var primaryBase)
                    || primaryBase is not Container primaryContainer)
                {
                    CancelPendingConstruction(user, pending);
                    return;
                }

                var storeContainers = new Dictionary<string, Container>();
                foreach (var (logical, containerId) in pending.StoreContainers)
                {
                    if (!_container.TryGetContainer(user, containerId, out var storeBase)
                        || storeBase is not Container storeContainer)
                    {
                        CancelPendingConstruction(user, pending);
                        return;
                    }

                    storeContainers[logical] = storeContainer;
                }

                var coords = pending.Coordinates;
                if (!coords.IsValid(EntityManager))
                {
                    CancelPendingConstruction(user, pending);
                    return;
                }

                var newEntityProto = edgeNode.Entity.GetId(null, user, new(EntityManager));
                var newEntity = SpawnAttachedTo(newEntityProto, coords, rotation: pending.Angle);

                if (!TryComp(newEntity, out ConstructionComponent? construction))
                {
                    Log.Error($"Initial construction does not have a valid target entity! It is missing a ConstructionComponent.\nGraph: {graph.ID}, Initial Target: {edge.Target}, Ent. Prototype: {newEntityProto}\nCreated Entity {ToPrettyString(newEntity)} will be deleted.");
                    Del(newEntity);
                    CancelPendingConstruction(user, pending);
                    return;
                }

                // We attempt to set the pathfinding target.
                SetPathfindingTarget(newEntity, targetNode.Name, construction);

                // We preserve the containers...
                foreach (var (name, cont) in storeContainers)
                {
                    var newCont = _container.EnsureContainer<Container>(newEntity, name);

                    foreach (var entity in cont.ContainedEntities.ToArray())
                    {
                        _container.Remove(entity, cont, reparent: false, force: true);
                        _container.Insert(entity, newCont);
                    }
                }

                // We now get rid of all them (consuming unstored materials).
                ShutdownConstructionContainers(primaryContainer, storeContainers.Values);

                // We have step completed steps!
                foreach (var step in edge.Steps)
                {
                    foreach (var completed in step.Completed)
                    {
                        completed.PerformAction(newEntity, user, EntityManager);
                    }
                }

                // And we also have edge completed effects!
                foreach (var completed in edge.Completed)
                {
                    completed.PerformAction(newEntity, user, EntityManager);
                }

                switch (pending.Kind)
                {
                    case InitialConstructionKind.Item:
                        // Just in case this is a stack, attempt to merge it. If it isn't a stack, this will just normally pick up
                        // or drop the item as normal.
                        _stackSystem.TryMergeToHands(newEntity, user);
                        break;
                    case InitialConstructionKind.Structure:
                        if (pending.StructureAck is { } ack
                            && pending.SessionUserId is { } userId
                            && _playerManager.TryGetSessionById(userId, out var session))
                        {
                            RaiseNetworkEvent(new AckStructureConstructionMessage(ack, GetNetEntity(newEntity)), session);
                        }

                        _adminLogger.Add(LogType.Construction, LogImpact.Low,
                            $"{ToPrettyString(user):player} has turned a {pending.ConstructionId} construction ghost into {ToPrettyString(newEntity)} at {Transform(newEntity).Coordinates}");
                        break;
                }

                ReleaseStructureAck(pending);
            }
            catch
            {
                CancelPendingConstruction(user, pending);
                throw;
            }
        }

        private void RestoreReservedMaterials(EntityUid user, string primaryContainerId, Dictionary<string, string> storeContainerIds)
        {
            if (!_container.TryGetContainer(user, primaryContainerId, out var primaryBase)
                || primaryBase is not Container primaryContainer)
            {
                return;
            }

            foreach (var entity in primaryContainer.ContainedEntities.ToArray())
            {
                _container.Remove(entity, primaryContainer);
            }

            var storeContainers = new List<Container>();
            foreach (var containerId in storeContainerIds.Values)
            {
                if (!_container.TryGetContainer(user, containerId, out var storeBase)
                    || storeBase is not Container storeContainer)
                    continue;

                foreach (var entity in storeContainer.ContainedEntities.ToArray())
                {
                    _container.Remove(entity, storeContainer);
                }

                storeContainers.Add(storeContainer);
            }

            // If we don't do this, items are invisible for some fucking reason. Nice.
            Timer.Spawn(1, () =>
            {
                if (!Exists(user))
                    return;

                ShutdownConstructionContainers(primaryContainer, storeContainers);
            });
        }

        private void ShutdownConstructionContainers(Container primary, IEnumerable<Container> stores)
        {
            if (primary.Owner.IsValid() && Exists(primary.Owner))
                _container.ShutdownContainer(primary);

            foreach (var c in stores.ToArray())
            {
                if (c.Owner.IsValid() && Exists(c.Owner))
                    _container.ShutdownContainer(c);
            }
        }

        private void ReleaseStructureAck(PendingInitialConstruction pending)
        {
            if (pending.StructureAck is not { } ack
                || pending.SessionUserId is not { } userId
                || !_playerManager.TryGetSessionById(userId, out var session))
                return;

            if (_beingBuilt.TryGetValue(session, out var set))
                set.Remove(ack);
        }

        private void HandleStartItemConstruction(TryStartItemConstructionMessage ev, EntitySessionEventArgs args)
        {
            if (args.SenderSession.AttachedEntity is {Valid: true} user)
                TryStartItemConstruction(ev.PrototypeName, user);
        }

        // LEGACY CODE. See warning at the top of the file!
        /// <summary>
        /// Validates and starts item construction. Returns true if materials were reserved and the DoAfter started.
        /// Final completion is asynchronous via <see cref="InitialConstructionDoAfterEvent"/>.
        /// </summary>
        public bool TryStartItemConstruction(string prototype, EntityUid user)
        {
            if (!PrototypeManager.TryIndex(prototype, out ConstructionPrototype? constructionPrototype))
            {
                Log.Error($"Tried to start construction of invalid recipe '{prototype}'!");
                return false;
            }

            if (!PrototypeManager.TryIndex(constructionPrototype.Graph,
                    out ConstructionGraphPrototype? constructionGraph))
            {
                Log.Error(
                    $"Invalid construction graph '{constructionPrototype.Graph}' in recipe '{prototype}'!");
                return false;
            }

            if (_whitelistSystem.IsWhitelistFail(constructionPrototype.EntityWhitelist, user))
            {
                _popup.PopupEntity(Loc.GetString("construction-system-cannot-start"), user, user);
                return false;
            }

            var startNode = constructionGraph.Nodes[constructionPrototype.StartNode];
            var targetNode = constructionGraph.Nodes[constructionPrototype.TargetNode];
            var pathFind = constructionGraph.Path(startNode.Name, targetNode.Name);

            if (!_actionBlocker.CanInteract(user, null))
                return false;

            if (!HasComp<HandsComponent>(user))
                return false;

            foreach (var condition in constructionPrototype.Conditions)
            {
                if (!condition.Condition(user, user.ToCoordinates(0, 0), Direction.South))
                    return false;
            }

            if (pathFind == null)
            {
                throw new InvalidDataException(
                    $"Can't find path from starting node to target node in construction! Recipe: {prototype}");
            }

            var edge = startNode.GetEdge(pathFind[0].Name);

            if (edge == null)
            {
                throw new InvalidDataException(
                    $"Can't find edge from starting node to the next node in pathfinding! Recipe: {prototype}");
            }

            // No support for conditions here!

            foreach (var step in edge.Steps)
            {
                switch (step)
                {
                    case ToolConstructionGraphStep _:
                        throw new InvalidDataException("Invalid first step for construction recipe!");
                }
            }

            return TryBeginConstruct(
                user,
                "item_construction",
                constructionGraph,
                edge,
                targetNode,
                Transform(user).Coordinates,
                default,
                InitialConstructionKind.Item,
                constructionPrototype.ID);
        }

        // LEGACY CODE. See warning at the top of the file!
        private void HandleStartStructureConstruction(TryStartStructureConstructionMessage ev, EntitySessionEventArgs args)
        {
            if (!PrototypeManager.TryIndex(ev.PrototypeName, out ConstructionPrototype? constructionPrototype))
            {
                Log.Error($"Tried to start construction of invalid recipe '{ev.PrototypeName}'!");
                RaiseNetworkEvent(new AckStructureConstructionMessage(ev.Ack));
                return;
            }

            if (!PrototypeManager.TryIndex(constructionPrototype.Graph, out ConstructionGraphPrototype? constructionGraph))
            {
                Log.Error($"Invalid construction graph '{constructionPrototype.Graph}' in recipe '{ev.PrototypeName}'!");
                RaiseNetworkEvent(new AckStructureConstructionMessage(ev.Ack));
                return;
            }

            if (args.SenderSession.AttachedEntity is not {Valid: true} user)
            {
                Log.Error($"Client sent {nameof(TryStartStructureConstructionMessage)} with no attached entity!");
                return;
            }

            if (_whitelistSystem.IsWhitelistFail(constructionPrototype.EntityWhitelist, user))
            {
                _popup.PopupEntity(Loc.GetString("construction-system-cannot-start"), user, user);
                return;
            }

            if (_container.IsEntityInContainer(user))
            {
                _popup.PopupEntity(Loc.GetString("construction-system-inside-container"), user, user);
                return;
            }

            var startNode = constructionGraph.Nodes[constructionPrototype.StartNode];
            var targetNode = constructionGraph.Nodes[constructionPrototype.TargetNode];
            var pathFind = constructionGraph.Path(startNode.Name, targetNode.Name);


            if (_beingBuilt.TryGetValue(args.SenderSession, out var set))
            {
                if (!set.Add(ev.Ack))
                {
                    _popup.PopupEntity(Loc.GetString("construction-system-already-building"), user, user);
                    return;
                }
            }
            else
            {
                var newSet = new HashSet<int> {ev.Ack};
                _beingBuilt[args.SenderSession] = newSet;
            }

            var location = GetCoordinates(ev.Location);

            foreach (var condition in constructionPrototype.Conditions)
            {
                if (!condition.Condition(user, location, ev.Angle.GetCardinalDir()))
                {
                    Cleanup();
                    return;
                }
            }

            void Cleanup()
            {
                _beingBuilt[args.SenderSession].Remove(ev.Ack);
            }

            if (!_actionBlocker.CanInteract(user, null)
                || !TryComp(user, out HandsComponent? hands) || _handsSystem.GetActiveItem((user, hands)) == null)
            {
                Cleanup();
                return;
            }

            var mapPos = _transformSystem.ToMapCoordinates(location);
            var predicate = GetPredicate(constructionPrototype.CanBuildInImpassable, mapPos);

            if (!_interactionSystem.InRangeUnobstructed(user, mapPos, predicate: predicate))
            {
                Cleanup();
                return;
            }

            if (pathFind == null)
                throw new InvalidDataException($"Can't find path from starting node to target node in construction! Recipe: {ev.PrototypeName}");

            var edge = startNode.GetEdge(pathFind[0].Name);

            if(edge == null)
                throw new InvalidDataException($"Can't find edge from starting node to the next node in pathfinding! Recipe: {ev.PrototypeName}");

            var valid = false;

            if (_handsSystem.GetActiveItem((user, hands)) is not {Valid: true} holding)
            {
                Cleanup();
                return;
            }

            // No support for conditions here!

            foreach (var step in edge.Steps)
            {
                switch (step)
                {
                    case EntityInsertConstructionGraphStep entityInsert:
                        if (entityInsert.EntityValid(holding, EntityManager, Factory))
                            valid = true;
                        break;
                    case ToolConstructionGraphStep _:
                        throw new InvalidDataException("Invalid first step for item recipe!");
                }

                if (valid)
                    break;
            }

            if (!valid)
            {
                Cleanup();
                return;
            }

            if (!TryBeginConstruct(
                    user,
                    (ev.Ack + constructionPrototype.GetHashCode()).ToString(),
                    constructionGraph,
                    edge,
                    targetNode,
                    GetCoordinates(ev.Location),
                    constructionPrototype.CanRotate ? ev.Angle : Angle.Zero,
                    InitialConstructionKind.Structure,
                    constructionPrototype.ID,
                    ev.Ack,
                    args.SenderSession.UserId))
            {
                Cleanup();
            }
        }
    }
}
