using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Funkystation.Footprints;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.Footprints;

public sealed class FootprintSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = null!;
    [Dependency] private readonly SharedMapSystem _map = null!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = null!;
    [Dependency] private readonly SharedPuddleSystem _puddle = null!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = null!;
    [Dependency] private readonly IRobustRandom _random = null!;
    [Dependency] private readonly InventorySystem _inventory = null!;

    private static readonly FixedPoint2 MaxVolumePerTile = 50;
    private static readonly EntProtoId FootprintEntityId = "Footprint";
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

        // Listen for chemical changes (like Space Cleaner)
        SubscribeLocalEvent<FootprintComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(EntityUid uid, FootprintComponent component, ref SolutionContainerChangedEvent args)
    {
        UpdatePrintColors(uid, component);
    }

    private void UpdatePrintColors(EntityUid uid, FootprintComponent component)
    {
        if (!_solutionContainer.TryGetSolution(uid, PrintSolutionName, out var solution, out _))
            return;

        var newBaseColor = solution.Value.Comp.Solution.GetColor(_prototypeManager);

        for (var i = 0; i < component.Prints.Count; i++)
        {
            var print = component.Prints[i];
            // Update the RGB while preserving the original alpha (transparency) of that specific step
            var updatedColor = newBaseColor.WithAlpha(print.Color.A);
            component.Prints[i] = print with { Color = updatedColor };
        }

        Dirty(uid, component);
        RaiseNetworkEvent(new FootprintStateEvent(GetNetEntity(uid)), Filter.Pvs(uid));
    }

    private void OnFootprintCleaned(EntityUid uid, FootprintComponent component, ref FootprintCleanEvent args)
    {
        TurnIntoPuddle(uid);
    }

    private void OnEntityMoved(EntityUid uid, FootprintOwnerComponent component, ref MoveEvent args)
    {
        if (HasComp<NoFootprintsComponent>(uid))
            return;

        if (_inventory.TryGetSlotEntity(uid, "shoes", out var shoes) && HasComp<NoFootprintsComponent>(shoes))
            return;

        if (!args.OldPosition.IsValid(EntityManager) || !args.NewPosition.IsValid(EntityManager))
            return;

        var prevPos = _transform.ToMapCoordinates(args.OldPosition).Position;
        var currentPos = _transform.ToMapCoordinates(args.NewPosition).Position;

        component.DistanceWalked += Vector2.Distance(currentPos, prevPos);

        var isStanding = !TryComp<StandingStateComponent>(uid, out var standing) || standing.Standing;
        var requiredDistance = isStanding ? component.FootstepDistance : component.DragDistance;

        if (component.DistanceWalked < requiredDistance)
            return;

        component.DistanceWalked -= requiredDistance;

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var oldLocal = _map.WorldToLocal(gridUid, grid, prevPos);
        var newLocal = _map.WorldToLocal(gridUid, grid, currentPos);
        var moveVector = newLocal - oldLocal;

        if (moveVector.LengthSquared() < 0.0001f)
            return;

        var walkAngle = moveVector.ToAngle();
        var rotation = walkAngle + Angle.FromDegrees(90);

        var stepOffset = isStanding ? component.AlternateStepOffset : 0f;
        component.AlternateStepOffset = -component.AlternateStepOffset;

        var rightVector = new Angle(walkAngle.Theta - Math.PI / 2).ToVec();
        var offsetPos = newLocal + rightVector * stepOffset;

        var coords = new EntityCoordinates(gridUid, offsetPos);
        var tileIndices = _map.CoordinatesToTile(gridUid, grid, coords);

        if (ProcessPuddleStepping(uid, component, gridUid, grid, tileIndices, isStanding))
            return;

        CreateFootprint(uid, component, gridUid, grid, tileIndices, coords, rotation, isStanding);
    }

    private bool ProcessPuddleStepping(EntityUid uid, FootprintOwnerComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i tile, bool isStanding)
    {
        if (!TryGetAnchoredPuddle(gridUid, grid, tile, out var puddleUid, out var puddle))
            return false;

        if (!_solutionContainer.TryGetSolution(puddleUid, PuddleTargetSolution, out var puddleSolution, out _))
            return false;

        var maxStorage = isStanding ? component.MaxFootVolume : component.MaxBodyVolume;

        if (!_solutionContainer.EnsureSolutionEntity(uid, PrintSolutionName, out _, out var ownerSolution, FixedPoint2.Max(component.MaxFootVolume, component.MaxBodyVolume)))
            return false;

        var amountToWash = CalculateTransferVolume(component, ownerSolution.Value, isStanding);
        _solutionContainer.TryTransferSolution(puddleSolution.Value, ownerSolution.Value.Comp.Solution, amountToWash);

        var spaceLeft = FixedPoint2.Max(0, maxStorage - ownerSolution.Value.Comp.Solution.Volume);
        _solutionContainer.TryTransferSolution(ownerSolution.Value, puddleSolution.Value.Comp.Solution, spaceLeft);

        _solutionContainer.UpdateChemicals(puddleSolution.Value, false);
        return true;
    }

    private void CreateFootprint(EntityUid uid, FootprintOwnerComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i tile, EntityCoordinates coords, Angle rotation, bool isStanding)
    {
        if (!_solutionContainer.TryGetSolution(uid, PrintSolutionName, out var ownerSolution, out _))
            return;

        var transferAmount = CalculateTransferVolume(component, ownerSolution.Value, isStanding);
        if (transferAmount < component.MinPrintVolume)
            return;

        if (!TryGetAnchoredFootprint(gridUid, grid, tile, out var printUid, out var printComp))
        {
            printUid = Spawn(FootprintEntityId, coords);
            printComp = Comp<FootprintComponent>(printUid);
        }

        if (!_solutionContainer.EnsureSolutionEntity(printUid, PrintSolutionName, out _, out var printSolution, MaxVolumePerTile))
            return;

        var maxVol = isStanding ? component.MaxFootprintVolume : component.MaxBodyprintVolume;
        var alpha = (float)transferAmount / maxVol / 2f;
        var color = ownerSolution.Value.Comp.Solution.GetColor(_prototypeManager).WithAlpha(alpha);

        _solutionContainer.TryTransferSolution(printSolution.Value, ownerSolution.Value.Comp.Solution, transferAmount);

        if (printSolution.Value.Comp.Solution.Volume >= MaxVolumePerTile)
        {
            var solClone = printSolution.Value.Comp.Solution.Clone();
            QueueDel(printUid);
            _puddle.TrySpillAt(coords, solClone, out _, false);
            return;
        }

        var localPosition = coords.Position;
        var normX = (localPosition.X / grid.TileSize) - MathF.Floor(localPosition.X / grid.TileSize) - (grid.TileSize / 2f);
        var normY = (localPosition.Y / grid.TileSize) - MathF.Floor(localPosition.Y / grid.TileSize) - (grid.TileSize / 2f);

        var state = isStanding ? "foot" : _random.Pick(DragStates);

        printComp.Prints.Add(new FootprintData(new Vector2(normX, normY), rotation, color, state));
        Dirty(printUid, printComp);

        RaiseNetworkEvent(new FootprintStateEvent(GetNetEntity(printUid)), Filter.Pvs(printUid));
    }

    private void OnPuddleInit(EntityUid uid, PuddleComponent component, ref MapInitEvent args)
    {
        if (HasComp<FootprintComponent>(uid))
            return;

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        if (TryGetAnchoredFootprint(gridUid, grid, tile, out var printUid, out _))
        {
            TurnIntoPuddle(printUid, xform.Coordinates);
        }
    }

    private void TurnIntoPuddle(EntityUid printUid, EntityCoordinates? coords = null)
    {
        var targetCoords = coords ?? Transform(printUid).Coordinates;

        if (_solutionContainer.TryGetSolution(printUid, PrintSolutionName, out _, out var printSolution))
        {
            var clone = printSolution.Clone();
            QueueDel(printUid);
            _puddle.TrySpillAt(targetCoords, clone, out _, false);
        }
        else
        {
            QueueDel(printUid);
        }
    }

    private FixedPoint2 CalculateTransferVolume(FootprintOwnerComponent component, Entity<SolutionComponent> sol, bool isStanding)
    {
        var vol = sol.Comp.Solution.Volume;
        if (isStanding)
        {
            var fraction = vol / component.MaxFootVolume;
            var spread = component.MaxFootprintVolume - component.MinPrintVolume;
            return FixedPoint2.Min(vol, (spread * fraction) + component.MinPrintVolume);
        }
        else
        {
            var fraction = vol / component.MaxBodyVolume;
            var spread = component.MaxBodyprintVolume - component.MinBodyPrintVolume;
            return FixedPoint2.Min(vol, (spread * fraction) + component.MinBodyPrintVolume);
        }
    }

    private bool TryGetAnchoredPuddle(EntityUid gridUid, MapGridComponent grid, Vector2i tile, out EntityUid entityUid, [NotNullWhen(true)] out PuddleComponent? component)
    {
        var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (enumerator.MoveNext(out var uid))
        {
            // CRITICAL: Explicitly ignore footprints so players don't wash their feet with them and delete them!
            if (HasComp<FootprintComponent>(uid))
                continue;

            if (TryComp(uid, out component))
            {
                entityUid = uid.Value;
                return true;
            }
        }
        entityUid = EntityUid.Invalid;
        component = null;
        return false;
    }

    private bool TryGetAnchoredFootprint(EntityUid gridUid, MapGridComponent grid, Vector2i tile, out EntityUid entityUid, [NotNullWhen(true)] out FootprintComponent? component)
    {
        var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (enumerator.MoveNext(out var uid))
        {
            if (TryComp(uid, out component))
            {
                entityUid = uid.Value;
                return true;
            }
        }
        entityUid = EntityUid.Invalid;
        component = null;
        return false;
    }
}
