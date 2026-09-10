using System.Numerics;
using Content.Shared._Funkystation.Footprints;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Footprints;

// Since we had to rewrite so much of this, I moved it to _Starlight.
public sealed partial class FootprintSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery = default!;
    [Dependency] private EntityQuery<FootprintComponent> _footprintQuery = default!;
    [Dependency] private EntityQuery<NoFootprintsComponent> _noFootprintsQuery = default!;
    [Dependency] private EntityQuery<SolutionComponent> _solutionQuery = default!;
    [Dependency] private EntityQuery<StandingStateComponent> _standingQuery = default!;

    // A footprint sprite layer is relatively expensive. Chemical volume may continue accumulating after this cap,
    // but no more visual/network state is added until the footprint reaches capacity and becomes a puddle.
    private const int MaxPrintsPerTile = 64;

    private static readonly EntProtoId _footprintEntityId = "Footprint";
    private static readonly EntProtoId _printSolutionEntityId = "SolutionPrint";
    private const string PrintSolutionName = "print";

    private static readonly FootprintVisualState[] _dragStates =
    [
        FootprintVisualState.Dragging1,
        FootprintVisualState.Dragging2,
        FootprintVisualState.Dragging3,
        FootprintVisualState.Dragging4,
        FootprintVisualState.Dragging5,
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FootprintComponent, FootprintCleanEvent>(OnFootprintCleaned);
        SubscribeLocalEvent<FootprintOwnerComponent, MoveEvent>(OnEntityMoved);
        SubscribeLocalEvent<FootprintOwnerComponent, MapInitEvent>(OnOwnerMapInit);
        SubscribeLocalEvent<FootprintOwnerComponent, EntRemovedFromContainerMessage>(OnOwnerSolutionRemoved);
        SubscribeLocalEvent<PuddleComponent, MapInitEvent>(OnPuddleInit);

        // Space cleaner and other chemical changes need to recolor existing visual layers.
        SubscribeLocalEvent<FootprintComponent, SolutionChangedEvent>(OnSolutionChanged,
            after: [typeof(SharedPuddleSystem)]);
    }

    private void OnSolutionChanged(Entity<FootprintComponent> entity, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id != PrintSolutionName)
            return;

        UpdatePrintColor(entity, args.Solution.Comp.Solution);
    }

    private void UpdatePrintColor(Entity<FootprintComponent> entity, Solution solution)
    {
        var baseColor = solution.GetColor(_prototypeManager).WithAlpha(1f);
        if (entity.Comp.BaseColor == baseColor)
            return;

        entity.Comp.BaseColor = baseColor;
        Dirty(entity);
    }

    private void OnOwnerMapInit(Entity<FootprintOwnerComponent> entity, ref MapInitEvent args)
    {
        if (_solutionContainer.TryGetSolution(entity.Owner, PrintSolutionName, out var solution))
            entity.Comp.Solution = solution;
    }

    private void OnOwnerSolutionRemoved(Entity<FootprintOwnerComponent> entity, ref EntRemovedFromContainerMessage args)
    {
        if (entity.Comp.Solution?.Owner == args.Entity)
            entity.Comp.Solution = null;
    }

    private void OnFootprintCleaned(Entity<FootprintComponent> entity, ref FootprintCleanEvent args) => TurnIntoPuddle(entity.Owner);

    private void OnEntityMoved(Entity<FootprintOwnerComponent> entity, ref MoveEvent args)
    {
        var hasMapPositions = false;
        var previousMapPosition = Vector2.Zero;
        var currentMapPosition = Vector2.Zero;
        Vector2 moveVector;
        var sameParent = args.OldPosition.EntityId == args.NewPosition.EntityId;

        // Normal movement stays parented directly to the same grid. Avoid four transform-tree conversions per
        // physics move in that overwhelmingly common case.
        if (sameParent)
        {
            moveVector = args.NewPosition.Position - args.OldPosition.Position;
            if (!float.IsFinite(moveVector.X) || !float.IsFinite(moveVector.Y))
                return;
        }
        else
        {
            if (!args.OldPosition.IsValid(EntityManager) || !args.NewPosition.IsValid(EntityManager))
                return;

            previousMapPosition = _transform.ToMapCoordinates(args.OldPosition).Position;
            currentMapPosition = _transform.ToMapCoordinates(args.NewPosition).Position;
            moveVector = currentMapPosition - previousMapPosition;
            hasMapPositions = true;
        }

        var distance = moveVector.Length();
        if (distance < 0.0001f)
            return;

        entity.Comp.DistanceWalked += distance;
        if (entity.Comp.DistanceWalked < MathF.Min(entity.Comp.FootstepDistance, entity.Comp.DragDistance))
            return;

        // Standing state only affects the print interval, so defer its lookup until either interval could be due.
        var isStanding = !_standingQuery.TryGetComponent(entity.Owner, out var standing) || standing.Standing;
        var requiredDistance = isStanding ? entity.Comp.FootstepDistance : entity.Comp.DragDistance;

        if (entity.Comp.DistanceWalked < requiredDistance)
            return;

        entity.Comp.DistanceWalked -= requiredDistance;

        // Equipment only changes occasionally, so do this lookup at print cadence rather than physics cadence.
        if (_noFootprintsQuery.HasComponent(entity.Owner) ||
            (_inventory.TryGetSlotEntity(entity.Owner, "shoes", out var shoes) &&
             _noFootprintsQuery.HasComponent(shoes)))
        {
            return;
        }

        var xform = Transform(entity.Owner);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        Vector2 newLocal;
        if (sameParent && args.NewPosition.EntityId == gridUid)
        {
            newLocal = args.NewPosition.Position;
        }
        else
        {
            if (!hasMapPositions)
            {
                if (!args.OldPosition.IsValid(EntityManager) || !args.NewPosition.IsValid(EntityManager))
                    return;

                previousMapPosition = _transform.ToMapCoordinates(args.OldPosition).Position;
                currentMapPosition = _transform.ToMapCoordinates(args.NewPosition).Position;
            }

            var oldLocal = _map.WorldToLocal(gridUid, grid, previousMapPosition);
            newLocal = _map.WorldToLocal(gridUid, grid, currentMapPosition);
            moveVector = newLocal - oldLocal;
        }

        var walkAngle = moveVector.ToAngle();
        var rotation = walkAngle + Angle.FromDegrees(90);

        var stepOffset = isStanding ? entity.Comp.AlternateStepOffset : 0f;
        entity.Comp.AlternateStepOffset = -entity.Comp.AlternateStepOffset;

        var rightVector = new Angle(walkAngle.Theta - (Math.PI / 2)).ToVec();
        var offsetPos = newLocal + (rightVector * stepOffset);

        var coords = new EntityCoordinates(gridUid, offsetPos);
        var tileIndices = _map.CoordinatesToTile(gridUid, grid, coords);

        FindTileFluids(gridUid, grid, tileIndices, out var puddle, out var footprint);

        if (ProcessPuddleStepping(entity, puddle, isStanding))
            return;

        CreateFootprint(entity, grid, footprint, coords, rotation, isStanding);
    }

    private bool ProcessPuddleStepping(
        Entity<FootprintOwnerComponent> entity,
        Entity<PuddleComponent>? puddle,
        bool isStanding)
    {
        if (puddle is not { } puddleEntity)
            return false;

        if (!_solutionContainer.ResolveSolution(
                puddleEntity.Owner,
                puddleEntity.Comp.SolutionName,
                ref puddleEntity.Comp.Solution,
                out var puddleSolution))
        {
            return false;
        }

        if (puddleSolution.Volume <= FixedPoint2.Zero)
            return false;

        if (!TryGetOrCreateCarriedSolution(entity, out var ownerSolution))
            return false;

        var maxStorage = isStanding ? entity.Comp.MaxFootVolume : entity.Comp.MaxBodyVolume;

        // Direct transfers keep the carried residue and the puddle chemically mixed without temporary spills.
        var amountToWash = CalculateTransferVolume(entity.Comp, ownerSolution, isStanding);
        var washedIntoPuddle = false;
        if (amountToWash > FixedPoint2.Zero)
        {
            washedIntoPuddle = _solutionContainer.TryTransferSolution(
                puddleEntity.Comp.Solution.Value,
                ownerSolution.Comp.Solution,
                amountToWash);
        }

        var spaceLeft = FixedPoint2.Max(FixedPoint2.Zero, maxStorage - ownerSolution.Comp.Solution.Volume);
        var refilledFromPuddle = false;
        if (spaceLeft > FixedPoint2.Zero)
        {
            refilledFromPuddle = _solutionContainer.TryTransferSolution(
                ownerSolution,
                puddleSolution,
                spaceLeft);
        }

        // TryTransferSolution only publishes its target, so publish either solution when it was only a source.
        if (washedIntoPuddle && !refilledFromPuddle)
            _solutionContainer.UpdateChemicals(ownerSolution, false);

        if (refilledFromPuddle)
            _solutionContainer.UpdateChemicals(puddleEntity.Comp.Solution.Value, false);

        return true;
    }

    private bool TryGetOrCreateCarriedSolution(
        Entity<FootprintOwnerComponent> entity,
        out Entity<SolutionComponent> ownerSolution)
    {
        if (TryGetCarriedSolution(entity, out ownerSolution))
            return true;

        var manager = EnsureComp<SolutionManagerComponent>(entity.Owner);
        var solutionContainer = _container.EnsureContainer<Container>(entity.Owner, manager.Container);
        ownerSolution = _solutionContainer.CreateSolution(_printSolutionEntityId, solutionContainer);
        entity.Comp.Solution = ownerSolution;

        // SolutionPrint is 50u for tiles. Carried residue only needs the owner's configured body/foot capacity.
        ownerSolution.Comp.Solution.MaxVolume = FixedPoint2.Max(
            entity.Comp.MaxFootVolume,
            entity.Comp.MaxBodyVolume);
        _solutionContainer.UpdateChemicals(ownerSolution, false);
        return true;
    }

    private bool TryGetCarriedSolution(
        Entity<FootprintOwnerComponent> entity,
        out Entity<SolutionComponent> ownerSolution)
    {
        if (entity.Comp.Solution is { } cached &&
            _solutionQuery.TryGetComponent(cached.Owner, out var solution))
        {
            ownerSolution = (cached.Owner, solution);
            entity.Comp.Solution = ownerSolution;
            return true;
        }

        // This fallback covers owners/components restored in an order where MapInit could not populate the cache.
        if (_solutionContainer.TryGetSolution(entity.Owner, PrintSolutionName, out var existing))
        {
            ownerSolution = existing.Value;
            entity.Comp.Solution = ownerSolution;
            return true;
        }

        ownerSolution = default;
        return false;
    }

    private void CreateFootprint(
        Entity<FootprintOwnerComponent> entity,
        MapGridComponent grid,
        Entity<FootprintComponent, PuddleComponent>? existingFootprint,
        EntityCoordinates coords,
        Angle rotation,
        bool isStanding)
    {
        if (!TryGetCarriedSolution(entity, out var ownerSolution))
            return;

        var transferAmount = CalculateTransferVolume(entity.Comp, ownerSolution, isStanding);
        var minimumVolume = isStanding ? entity.Comp.MinPrintVolume : entity.Comp.MinBodyPrintVolume;
        if (transferAmount < minimumVolume)
            return;

        var spawned = false;
        Entity<FootprintComponent, PuddleComponent> footprint;
        if (existingFootprint is not { } existing)
        {
            var printUid = Spawn(_footprintEntityId, coords);
            footprint = (printUid, Comp<FootprintComponent>(printUid), Comp<PuddleComponent>(printUid));
            spawned = true;
        }
        else
        {
            footprint = existing;
        }

        if (!_solutionContainer.ResolveSolution(
                footprint.Owner,
                footprint.Comp2.SolutionName,
                ref footprint.Comp2.Solution,
                out var printSolution))
        {
            if (spawned)
                QueueDel(footprint.Owner);

            return;
        }

        if (!_solutionContainer.TryTransferSolution(
                footprint.Comp2.Solution.Value,
                ownerSolution.Comp.Solution,
                transferAmount))
        {
            if (spawned)
                QueueDel(footprint.Owner);

            return;
        }

        // The transfer call publishes the footprint target, but not the carried source solution.
        _solutionContainer.UpdateChemicals(ownerSolution, false);

        // The prototype capacity is the conversion threshold, so these values cannot drift apart.
        if (printSolution.Volume >= printSolution.MaxVolume)
        {
            TurnIntoPuddle(footprint.Owner, coords, footprint.Comp2);
            return;
        }

        // Still transfer chemistry at the cap, but never grow the replicated sprite-layer list past it.
        if (footprint.Comp1.Prints.Count >= MaxPrintsPerTile)
            return;

        var maxVisualVolume = isStanding
            ? entity.Comp.MaxFootprintVolume
            : entity.Comp.MaxBodyprintVolume;
        var alpha = maxVisualVolume > FixedPoint2.Zero
            ? (float) transferAmount / maxVisualVolume / 2f
            : 0f;
        var localPosition = coords.Position;
        var normX = (localPosition.X / grid.TileSize) -
                    MathF.Floor(localPosition.X / grid.TileSize) -
                    (grid.TileSize / 2f);
        var normY = (localPosition.Y / grid.TileSize) -
                    MathF.Floor(localPosition.Y / grid.TileSize) -
                    (grid.TileSize / 2f);

        var state = isStanding ? FootprintVisualState.Foot : _random.Pick(_dragStates);

        footprint.Comp1.Prints.Add(new FootprintData(new Vector2(normX, normY), rotation, alpha, state));
        Dirty(footprint.Owner, footprint.Comp1);
    }

    private void OnPuddleInit(Entity<PuddleComponent> entity, ref MapInitEvent args)
    {
        if (_footprintQuery.HasComponent(entity.Owner))
            return;

        var xform = Transform(entity.Owner);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        FindTileFluids(gridUid, grid, tile, out _, out var footprint);
        if (footprint is { } print)
            TurnIntoPuddle(print.Owner, xform.Coordinates, print.Comp2);
    }

    private void TurnIntoPuddle(
        EntityUid printUid,
        EntityCoordinates? coords = null,
        PuddleComponent? puddle = null)
    {
        var targetCoords = coords ?? Transform(printUid).Coordinates;

        if (_puddleQuery.Resolve(printUid, ref puddle, false) &&
            _solutionContainer.ResolveSolution(printUid, puddle.SolutionName, ref puddle.Solution, out var printSolution))
        {
            var clone = printSolution.Clone();
            QueueDel(printUid);
            _puddle.TrySpillAt(targetCoords, clone, out _, false);
            return;
        }

        QueueDel(printUid);
    }

    private static FixedPoint2 CalculateTransferVolume(
        FootprintOwnerComponent component,
        Entity<SolutionComponent> solution,
        bool isStanding)
    {
        var volume = solution.Comp.Solution.Volume;
        var maxVolume = isStanding ? component.MaxFootVolume : component.MaxBodyVolume;
        if (maxVolume <= FixedPoint2.Zero || volume <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var maxPrintVolume = isStanding ? component.MaxFootprintVolume : component.MaxBodyprintVolume;
        var minPrintVolume = isStanding ? component.MinPrintVolume : component.MinBodyPrintVolume;
        var fraction = volume / maxVolume;
        var spread = maxPrintVolume - minPrintVolume;

        return FixedPoint2.Max(
            FixedPoint2.Zero,
            FixedPoint2.Min(volume, (spread * fraction) + minPrintVolume));
    }

    private void FindTileFluids(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        out Entity<PuddleComponent>? puddle,
        out Entity<FootprintComponent, PuddleComponent>? footprint)
    {
        puddle = null;
        footprint = null;

        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!_puddleQuery.TryGetComponent(uid.Value, out var puddleComponent))
                continue;

            // A footprint carries PuddleComponent for cleaning, but is not a puddle to wash feet in.
            if (_footprintQuery.TryGetComponent(uid.Value, out var footprintComponent))
            {
                footprint = (uid.Value, footprintComponent, puddleComponent);
                if (puddle != null)
                    return;

                continue;
            }

            puddle = (uid.Value, puddleComponent);
            if (footprint != null)
                return;
        }
    }
}
