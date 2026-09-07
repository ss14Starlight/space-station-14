using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.Random.Helpers;
using Content.Shared.Standing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Funkystation.Footprints;

public sealed partial class FootprintSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedMapSystem _map = null!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = null!;
    [Dependency] private SharedPuddleSystem _puddle = null!;
    [Dependency] private IPrototypeManager _prototypeManager = null!;
    [Dependency] private InventorySystem _inventory = null!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;


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

        // Listen for chemical changes (like Space Cleaner)
        // Moff, after sharedpuddle
        SubscribeLocalEvent<FootprintComponent, SolutionChangedEvent>(OnSolutionChanged, after: [typeof(SharedPuddleSystem)]);
    }

    private void OnSolutionChanged(EntityUid uid, FootprintComponent component, ref SolutionChangedEvent args)
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
    }

    [SubscribeLocalEvent]
    private void OnFootprintCleaned(Entity<FootprintComponent> ent, ref FootprintCleanEvent args)
    {
        TurnIntoPuddle(ent.Owner);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnEntityMoved(Entity<FootprintOwnerComponent> ent, ref MoveEvent args)
    {
        if (HasComp<NoFootprintsComponent>(ent.Owner))
            return;

        if (_inventory.TryGetSlotEntity(ent.Owner, "shoes", out var shoes) && HasComp<NoFootprintsComponent>(shoes))
            return;

        if (!args.OldPosition.IsValid(EntityManager) || !args.NewPosition.IsValid(EntityManager))
            return;

        var prevPos = _transform.ToMapCoordinates(args.OldPosition).Position;
        var currentPos = _transform.ToMapCoordinates(args.NewPosition).Position;

        ent.Comp.DistanceWalked += Vector2.Distance(currentPos, prevPos);

        var isStanding = !TryComp<StandingStateComponent>(ent.Owner, out var standing) || standing.Standing;
        var requiredDistance = isStanding ? ent.Comp.FootstepDistance : ent.Comp.DragDistance;

        if (ent.Comp.DistanceWalked < requiredDistance)
            return;

        ent.Comp.DistanceWalked -= requiredDistance;

        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var oldLocal = _map.WorldToLocal(gridUid, grid, prevPos);
        var newLocal = _map.WorldToLocal(gridUid, grid, currentPos);
        var moveVector = newLocal - oldLocal;

        if (moveVector.LengthSquared() < 0.0001f)
            return;

        var walkAngle = moveVector.ToAngle();
        var rotation = walkAngle + Angle.FromDegrees(90);

        var stepOffset = isStanding ? ent.Comp.AlternateStepOffset : 0f;
        ent.Comp.AlternateStepOffset = -ent.Comp.AlternateStepOffset;

        var rightVector = new Angle(walkAngle.Theta - Math.PI / 2).ToVec();
        var offsetPos = newLocal + rightVector * stepOffset;

        var coords = new EntityCoordinates(gridUid, offsetPos);
        var tileIndices = _map.CoordinatesToTile(gridUid, grid, coords);

        if (ProcessPuddleStepping(ent.Owner, ent.Comp, gridUid, grid, tileIndices, isStanding))
            return;

        CreateFootprint(ent.Owner, ent.Comp, gridUid, grid, tileIndices, coords, rotation, isStanding);
    }

    private bool ProcessPuddleStepping(EntityUid uid, FootprintOwnerComponent component, EntityUid gridUid, MapGridComponent grid, Vector2i tile, bool isStanding)
    {
        if (!TryGetAnchoredPuddle(gridUid, grid, tile, out var puddleUid, out var puddle))
            return false;

        if (!_solutionContainer.TryGetSolution(puddleUid, PuddleTargetSolution, out var puddleSolution, out _))
            return false;

        var maxStorage = isStanding ? component.MaxFootVolume : component.MaxBodyVolume;

        // Moff start - Use a non-deprecated method to get the solution
        if (!_solutionContainer.TryGetSolution(uid, PrintSolutionName, out var s) || s is not {} ownerSolution)
            return false;
        // Moff End

        if (maxStorage - ownerSolution.Comp.Solution.Volume <= 0)
        {
            var split = _solutionContainer.SplitSolution(ownerSolution, component.PrintMixAmount);
            _puddle.TrySpillAt(Transform(puddleUid).Coordinates, split, out _, false);
        }
        var spaceLeft = FixedPoint2.Max(0, maxStorage - ownerSolution.Comp.Solution.Volume);
        _solutionContainer.TryTransferSolution(ownerSolution, puddleSolution.Value.Comp.Solution, spaceLeft);
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
            printUid = PredictedSpawnAtPosition(FootprintEntityId, coords);
            printComp = Comp<FootprintComponent>(printUid);
        }

        // Moff start - Use a non-deprecated method to get the solution
        if (_solutionContainer.EnumerateSolutions(printUid)
                .Where(s => s.Name == PrintSolutionName)
                .FirstOrNull() is not { } printSolution)
            return;
        // Moff end

        // Moff start - Make alpha calulation better
        // Calculate colors and volume
        var minVol = isStanding ? component.MinPrintVolume : component.MinBodyPrintVolume;
        var maxVol = isStanding ? component.MaxFootprintVolume : component.MaxBodyprintVolume;
        var alpha = MathHelper.Clamp01((transferAmount.Float() - minVol) / (maxVol - minVol));
        var color = ownerSolution.Value.Comp.Solution.GetColor(_prototypeManager).WithAlpha(alpha);
        // Moff end

        _solutionContainer.TryTransferSolution(printSolution.Solution, ownerSolution.Value.Comp.Solution, transferAmount);

        if (printSolution.Solution.Comp.Solution.Volume >= MaxVolumePerTile)
        {
            var solClone = printSolution.Solution.Comp.Solution.Clone();
            PredictedQueueDel(printUid);
            _puddle.TrySpillAt(coords, solClone, out _, false);
            return;
        }

        var localPosition = coords.Position;
        var normX = (localPosition.X / grid.TileSize) - MathF.Floor(localPosition.X / grid.TileSize) - (grid.TileSize / 2f);
        var normY = (localPosition.Y / grid.TileSize) - MathF.Floor(localPosition.Y / grid.TileSize) - (grid.TileSize / 2f);

        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(uid));
        var state = isStanding ? "foot" : random.Pick(DragStates);

        printComp.Prints.Add(new FootprintData(new Vector2(normX, normY), rotation, color, state));
        Dirty(printUid, printComp);
    }

    [SubscribeLocalEvent]
    private void OnPuddleInit(Entity<PuddleComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<FootprintComponent>(ent.Owner))
            return;

        var xform = Transform(ent.Owner);
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
            PredictedQueueDel(printUid);
            _puddle.TrySpillAt(targetCoords, clone, out _, false);
        }
        else
        {
            PredictedQueueDel(printUid);
        }
    }

    private FixedPoint2 CalculateTransferVolume(FootprintOwnerComponent component, Entity<SolutionComponent> sol, bool isStanding)
    {
        var vol = sol.Comp.Solution.Volume;
        var maxVolume = isStanding ? component.MaxFootVolume : component.MaxBodyVolume;
        var maxPrintVolume = isStanding ? component.MaxFootprintVolume : component.MaxBodyprintVolume;
        var minPrintVolume = isStanding ? component.MinPrintVolume : component.MinBodyPrintVolume;

        var fraction = vol / maxVolume;
        var spread = maxPrintVolume - minPrintVolume;
        return FixedPoint2.Max(FixedPoint2.Min(vol, (spread * fraction) + minPrintVolume), 0f);
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
