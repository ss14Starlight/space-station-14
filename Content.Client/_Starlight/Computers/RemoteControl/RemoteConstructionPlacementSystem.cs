using System.Numerics;
using Content.Client.Construction;
using Content.Shared.Atmos.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteConstructionPlacementSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private ConstructionSystem _constructionSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private ConstructionPrototype? _prototype;
    private ConstructionPrototype? _lastPlacementPrototype;
    private EntityUid? _ghost;
    private EntityCoordinates? _lastLocation;

    public bool IsActive => _prototype is not null;

    public override void Initialize()
    {
        base.Initialize();
        EntityManager.System<RemoteControlInterface>().ControlledEntityChanged += OnControlledEntityChanged;
    }

    public override void Shutdown()
    {
        EntityManager.System<RemoteControlInterface>().ControlledEntityChanged -= OnControlledEntityChanged;
        Clear();
        base.Shutdown();
    }

    private void OnControlledEntityChanged(EntityUid? entity)
    {
        if (entity is null)
            Clear();
    }

    public bool TryBegin(ConstructionPrototype prototype)
    {
        if (EntityManager.System<RemoteControlInterface>().ControlledEntity is null
            || prototype.Type != ConstructionType.Structure)
            return false;

        Clear();
        _prototype = prototype;
        return true;
    }

    public void Clear()
    {
        if (_ghost is { } ghost && !Deleted(ghost))
            _constructionSystem.ClearGhost(ghost.GetHashCode());

        _ghost = null;
        _lastLocation = null;
        _lastPlacementPrototype = null;
        _prototype = null;
    }

    public bool TryCommit(EntityUid? target)
    {
        if (_prototype is null || _ghost is not { } ghost || target != ghost || Deleted(ghost))
            return false;

        _ghost = null;
        _lastLocation = null;
        _prototype = null;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_prototype is not { } prototype
            || EntityManager.System<RemoteControlInterface>().ControlledEntity is not { } user
            || EntityManager.System<RemoteControlInterface>().RemoteMousePosition is not { } mouse)
            return;

        var (location, placementPrototype) = GetPlacement(prototype, mouse);
        if (_lastLocation == location && _lastPlacementPrototype == placementPrototype)
            return;

        if (_ghost is { } oldGhost && !Deleted(oldGhost))
            _constructionSystem.ClearGhost(oldGhost.GetHashCode());

        _ghost = null;
        _lastLocation = location;
        _lastPlacementPrototype = placementPrototype;

        if (_constructionSystem.TrySpawnGhost(
                placementPrototype, location, Direction.South, user, out var ghost, showPopup: false))
            _ghost = ghost;
    }

    private (EntityCoordinates Location, ConstructionPrototype Prototype) GetPlacement(
        ConstructionPrototype prototype, MapCoordinates mouse)
    {
        if (!_mapSystem.TryFindGridAt(mouse, out var grid, out _)
            || !grid.IsValid()
            || prototype.PlacementMode is not ("SnapgridCenter" or "AlignAtmosPipeLayers"))
            return (_transformSystem.ToCoordinates(mouse), prototype);

        var gridComponent = Comp<MapGridComponent>(grid);
        var rawLocal = _transformSystem.ToCoordinates(grid, mouse).Position;
        var tileSize = gridComponent.TileSize;
        var snapped = new Vector2(
            (float) (MathF.Round((rawLocal.X / tileSize) - 0.5f, MidpointRounding.AwayFromZero) + 0.5f) * tileSize,
            (float) (MathF.Round((rawLocal.Y / tileSize) - 0.5f, MidpointRounding.AwayFromZero) + 0.5f) * tileSize);

        var location = new EntityCoordinates(grid, snapped);
        if (prototype.PlacementMode != "AlignAtmosPipeLayers"
            || prototype.AlternativePrototypes.Length == 0)
            return (location, prototype);

        var layer = GetPipeLayer(rawLocal - snapped, grid);
        if ((int) layer >= prototype.AlternativePrototypes.Length
            || !_prototypeManager.TryIndex(prototype.AlternativePrototypes[(int) layer], out ConstructionPrototype? alternative))
            return (location, prototype);

        return (location, alternative);
    }

    private AtmosPipeLayer GetPipeLayer(Vector2 mouseOffset, EntityUid grid)
    {
        const float InnerRadius = 0.125f;
        var distance = mouseOffset.Length();
        if (distance <= InnerRadius)
            return AtmosPipeLayer.Primary;

        var gridRotation = _transformSystem.GetWorldRotation(grid);
        var eyeRotation = EntityManager.System<RemoteControlInterface>().ControlledEye?.Rotation ?? Angle.Zero;
        var direction = (new Angle(mouseOffset) + eyeRotation + gridRotation + (Math.PI / 2)).GetCardinalDir();
        return distance > InnerRadius * 2
            ? direction is Direction.North or Direction.East ? AtmosPipeLayer.Quaternary : AtmosPipeLayer.Quinary
            : direction is Direction.North or Direction.East ? AtmosPipeLayer.Secondary : AtmosPipeLayer.Tertiary;
    }
}
