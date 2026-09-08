using System.Diagnostics.CodeAnalysis;
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

    // A footprint sprite layer is relatively expensive. Chemical volume may continue accumulating after this cap,
    // but no more visual/network state is added until the footprint reaches capacity and becomes a puddle.
    private const int MaxPrintsPerTile = 64;

    private static readonly EntProtoId FootprintEntityId = "Footprint";
    private static readonly EntProtoId PrintSolutionEntityId = "SolutionPrint";
    private const string PrintSolutionName = "print";
    private const string PuddleTargetSolution = "puddle";

    private static readonly string[] DragStates =
    [
        "dragging-1",
        "dragging-2",
        "dragging-3",
        "dragging-4",
        "dragging-5"
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FootprintComponent, FootprintCleanEvent>(OnFootprintCleaned);
        SubscribeLocalEvent<FootprintOwnerComponent, MoveEvent>(OnEntityMoved);
        SubscribeLocalEvent<PuddleComponent, MapInitEvent>(OnPuddleInit);

        // Space cleaner and other chemical changes need to recolor existing visual layers.
        SubscribeLocalEvent<FootprintComponent, SolutionChangedEvent>(OnSolutionChanged,
            after: [typeof(SharedPuddleSystem)]);
    }

    private void OnSolutionChanged(Entity<FootprintComponent> entity, ref SolutionChangedEvent args)
    {
        UpdatePrintColors(entity);
    }

    private void UpdatePrintColors(Entity<FootprintComponent> entity)
    {
        if (!_solutionContainer.TryGetSolution(entity.Owner, PrintSolutionName, out var solution, out _))
            return;

        var newBaseColor = solution.Value.Comp.Solution.GetColor(_prototypeManager);
        var changed = false;

        for (var i = 0; i < entity.Comp.Prints.Count; i++)
        {
            var print = entity.Comp.Prints[i];
            var updatedColor = newBaseColor.WithAlpha(print.Color.A);
            if (print.Color == updatedColor)
                continue;

            entity.Comp.Prints[i] = print with { Color = updatedColor };
            changed = true;
        }

        if (changed)
            Dirty(entity);
    }

    private void OnFootprintCleaned(Entity<FootprintComponent> entity, ref FootprintCleanEvent args)
    {
        TurnIntoPuddle(entity.Owner);
    }

    private void OnEntityMoved(Entity<FootprintOwnerComponent> entity, ref MoveEvent args)
    {
        if (_noFootprintsQuery.HasComponent(entity.Owner))
            return;

        if (_inventory.TryGetSlotEntity(entity.Owner, "shoes", out var shoes) &&
            _noFootprintsQuery.HasComponent(shoes))
        {
            return;
        }

        if (!args.OldPosition.IsValid(EntityManager) || !args.NewPosition.IsValid(EntityManager))
            return;

        var prevPos = _transform.ToMapCoordinates(args.OldPosition).Position;
        var currentPos = _transform.ToMapCoordinates(args.NewPosition).Position;

        entity.Comp.DistanceWalked += Vector2.Distance(currentPos, prevPos);

        var isStanding = !TryComp<StandingStateComponent>(entity.Owner, out var standing) || standing.Standing;
        var requiredDistance = isStanding ? entity.Comp.FootstepDistance : entity.Comp.DragDistance;

        if (entity.Comp.DistanceWalked < requiredDistance)
            return;

        entity.Comp.DistanceWalked -= requiredDistance;

        var xform = Transform(entity.Owner);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var oldLocal = _map.WorldToLocal(gridUid, grid, prevPos);
        var newLocal = _map.WorldToLocal(gridUid, grid, currentPos);
        var moveVector = newLocal - oldLocal;

        if (moveVector.LengthSquared() < 0.0001f)
            return;

        var walkAngle = moveVector.ToAngle();
        var rotation = walkAngle + Angle.FromDegrees(90);

        var stepOffset = isStanding ? entity.Comp.AlternateStepOffset : 0f;
        entity.Comp.AlternateStepOffset = -entity.Comp.AlternateStepOffset;

        var rightVector = new Angle(walkAngle.Theta - Math.PI / 2).ToVec();
        var offsetPos = newLocal + rightVector * stepOffset;

        var coords = new EntityCoordinates(gridUid, offsetPos);
        var tileIndices = _map.CoordinatesToTile(gridUid, grid, coords);

        if (ProcessPuddleStepping(entity, gridUid, grid, tileIndices, isStanding))
            return;

        CreateFootprint(entity, gridUid, grid, tileIndices, coords, rotation, isStanding);
    }

    private bool ProcessPuddleStepping(
        Entity<FootprintOwnerComponent> entity,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        bool isStanding)
    {
        if (!TryGetAnchoredPuddle(gridUid, grid, tile, out var puddleUid, out _))
            return false;

        if (!_solutionContainer.TryGetSolution(puddleUid, PuddleTargetSolution, out var puddleSolution, out _))
            return false;

        if (puddleSolution.Value.Comp.Solution.Volume <= FixedPoint2.Zero)
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
                puddleSolution.Value,
                ownerSolution.Comp.Solution,
                amountToWash);
        }

        var spaceLeft = FixedPoint2.Max(FixedPoint2.Zero, maxStorage - ownerSolution.Comp.Solution.Volume);
        var refilledFromPuddle = false;
        if (spaceLeft > FixedPoint2.Zero)
        {
            refilledFromPuddle = _solutionContainer.TryTransferSolution(
                ownerSolution,
                puddleSolution.Value.Comp.Solution,
                spaceLeft);
        }

        // TryTransferSolution only publishes its target, so publish either solution when it was only a source.
        if (washedIntoPuddle && !refilledFromPuddle)
            _solutionContainer.UpdateChemicals(ownerSolution, false);

        if (refilledFromPuddle)
            _solutionContainer.UpdateChemicals(puddleSolution.Value, false);

        return true;
    }

    private bool TryGetOrCreateCarriedSolution(
        Entity<FootprintOwnerComponent> entity,
        out Entity<SolutionComponent> ownerSolution)
    {
        if (_solutionContainer.TryGetSolution(entity.Owner, PrintSolutionName, out var existing))
        {
            ownerSolution = existing.Value;
            return true;
        }

        var manager = EnsureComp<SolutionManagerComponent>(entity.Owner);
        var solutionContainer = _container.EnsureContainer<Container>(entity.Owner, manager.Container);
        ownerSolution = _solutionContainer.CreateSolution(PrintSolutionEntityId, solutionContainer);

        // SolutionPrint is 50u for tiles. Carried residue only needs the owner's configured body/foot capacity.
        ownerSolution.Comp.Solution.MaxVolume = FixedPoint2.Max(
            entity.Comp.MaxFootVolume,
            entity.Comp.MaxBodyVolume);
        _solutionContainer.UpdateChemicals(ownerSolution, false);
        return true;
    }

    private void CreateFootprint(
        Entity<FootprintOwnerComponent> entity,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        EntityCoordinates coords,
        Angle rotation,
        bool isStanding)
    {
        if (!_solutionContainer.TryGetSolution(entity.Owner, PrintSolutionName, out var ownerSolution, out _))
            return;

        var transferAmount = CalculateTransferVolume(entity.Comp, ownerSolution.Value, isStanding);
        var minimumVolume = isStanding ? entity.Comp.MinPrintVolume : entity.Comp.MinBodyPrintVolume;
        if (transferAmount < minimumVolume)
            return;

        var spawned = false;
        if (!TryGetAnchoredFootprint(gridUid, grid, tile, out var printUid, out var printComp))
        {
            printUid = Spawn(FootprintEntityId, coords);
            printComp = Comp<FootprintComponent>(printUid);
            spawned = true;
        }

        if (!_solutionContainer.TryGetSolution(printUid, PrintSolutionName, out var printSolution, out _))
        {
            if (spawned)
                QueueDel(printUid);

            return;
        }

        // Capture the source color before moving the reagents, especially if this step empties the carrier.
        var baseColor = ownerSolution.Value.Comp.Solution.GetColor(_prototypeManager);
        if (!_solutionContainer.TryTransferSolution(
                printSolution.Value,
                ownerSolution.Value.Comp.Solution,
                transferAmount))
        {
            if (spawned)
                QueueDel(printUid);

            return;
        }

        // The transfer call publishes the footprint target, but not the carried source solution.
        _solutionContainer.UpdateChemicals(ownerSolution.Value, false);

        // The prototype capacity is the conversion threshold, so these values cannot drift apart.
        if (printSolution.Value.Comp.Solution.Volume >= printSolution.Value.Comp.Solution.MaxVolume)
        {
            TurnIntoPuddle(printUid, coords);
            return;
        }

        // Still transfer chemistry at the cap, but never grow the replicated sprite-layer list past it.
        if (printComp.Prints.Count >= MaxPrintsPerTile)
            return;

        var maxVisualVolume = isStanding
            ? entity.Comp.MaxFootprintVolume
            : entity.Comp.MaxBodyprintVolume;
        var alpha = maxVisualVolume > FixedPoint2.Zero
            ? (float) transferAmount / maxVisualVolume / 2f
            : 0f;
        var color = baseColor.WithAlpha(alpha);

        var localPosition = coords.Position;
        var normX = localPosition.X / grid.TileSize -
                    MathF.Floor(localPosition.X / grid.TileSize) -
                    grid.TileSize / 2f;
        var normY = localPosition.Y / grid.TileSize -
                    MathF.Floor(localPosition.Y / grid.TileSize) -
                    grid.TileSize / 2f;

        var state = isStanding ? "foot" : _random.Pick(DragStates);

        printComp.Prints.Add(new FootprintData(new Vector2(normX, normY), rotation, color, state));
        Dirty(printUid, printComp);
    }

    private void OnPuddleInit(Entity<PuddleComponent> entity, ref MapInitEvent args)
    {
        if (_footprintQuery.HasComponent(entity.Owner))
            return;

        var xform = Transform(entity.Owner);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        if (TryGetAnchoredFootprint(gridUid, grid, tile, out var printUid, out _))
            TurnIntoPuddle(printUid, xform.Coordinates);
    }

    private void TurnIntoPuddle(EntityUid printUid, EntityCoordinates? coords = null)
    {
        var targetCoords = coords ?? Transform(printUid).Coordinates;

        if (_solutionContainer.TryGetSolution(printUid, PrintSolutionName, out _, out var printSolution))
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
            FixedPoint2.Min(volume, spread * fraction + minPrintVolume));
    }

    private bool TryGetAnchoredPuddle(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        out EntityUid entityUid,
        [NotNullWhen(true)] out PuddleComponent? component)
    {
        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            // A footprint carries PuddleComponent for cleaning, but is not a puddle to wash feet in.
            if (_footprintQuery.HasComponent(uid.Value))
                continue;

            if (!_puddleQuery.TryGetComponent(uid.Value, out component))
                continue;

            entityUid = uid.Value;
            return true;
        }

        entityUid = EntityUid.Invalid;
        component = null;
        return false;
    }

    private bool TryGetAnchoredFootprint(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        out EntityUid entityUid,
        [NotNullWhen(true)] out FootprintComponent? component)
    {
        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!_footprintQuery.TryGetComponent(uid.Value, out component))
                continue;

            entityUid = uid.Value;
            return true;
        }

        entityUid = EntityUid.Invalid;
        component = null;
        return false;
    }
}
