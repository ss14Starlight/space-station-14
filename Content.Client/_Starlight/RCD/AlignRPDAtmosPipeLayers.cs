using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;
using static Robust.Client.Placement.PlacementManager;
using Content.Shared.Atmos.EntitySystems;

namespace Content.Client._Starlight.RCD;

/// <summary>
/// Funkystation
/// Allows users to place RCD prototypes with atmos pipe layers on different layers depending on how the mouse cursor is positioned within a grid tile.
/// </summary>
/// <remarks>
/// This placement mode is not on the engine because it is content specific.
/// </remarks>
public sealed class AlignRPDAtmosPipeLayers : PlacementMode
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IEntityNetworkManager _entityNetwork = default!;

    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _transformSystem;
    private readonly SharedAtmosPipeLayersSystem _pipeLayersSystem;
    private readonly SpriteSystem _spriteSystem;
    private readonly RCDSystem _rcdSystem;
    private readonly HandsSystem _handsSystem;

    private const float SearchBoxSize = 2f;
    private const float MouseDeadzoneRadius = 0.25f;
    private const float PlaceColorBaseAlpha = 0.5f;
    private const float GuideRadius = 0.05f;
    private const float GuideOffset = 0.125f;

    private EntityCoordinates _mouseCoordsRaw = default;
    private AtmosPipeLayer _currentLayer = AtmosPipeLayer.Primary;
    private EntityUid? _lastLayerSyncEntity = null;
    private AtmosPipeLayer? _lastLayerSynced = null;
    private Color _guideColor = new(0, 0, 0.5785f);

    public AlignRPDAtmosPipeLayers(PlacementManager pMan) : base(pMan)
    {
        IoCManager.InjectDependencies(this);
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _rcdSystem = _entityManager.System<RCDSystem>();
        _pipeLayersSystem = _entityManager.System<SharedAtmosPipeLayersSystem>();
        _handsSystem = _entityManager.System<HandsSystem>();
        ValidPlaceColor = ValidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
    }

    public override void Render(in OverlayDrawArgs args)
    {
        // Early exit if mouse is out of interaction range
        if (_playerManager.LocalSession?.AttachedEntity is not { } player ||
            !_entityManager.TryGetComponent<TransformComponent>(player, out var xform) ||
            !_transformSystem.InRange(xform.Coordinates, MouseCoords, SharedInteractionSystem.InteractionRange))
        {
            return;
        }

        var gridUid = _transformSystem.GetGrid(MouseCoords);

        if (gridUid == null || !_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
            return;

        if (!_handsSystem.TryGetActiveItem(player, out var heldEntity) ||
            !_entityManager.TryGetComponent<RCDComponent>(heldEntity, out var rcd))
            return;

        // Draw guide circles for each pipe layer if we are not in line/grid placing mode
        if (rcd.CurrentMode == RpdMode.Free && pManager.PlacementType == PlacementTypes.None )
        {
            var gridRotation = _transformSystem.GetWorldRotation(gridUid.Value);
            var worldPosition = _mapSystem.LocalToWorld(gridUid.Value, grid, MouseCoords.Position);
            var direction = (_eyeManager.CurrentEye.Rotation + gridRotation + (Math.PI / 2)).GetCardinalDir();
            var multi = (direction is Direction.North or Direction.South) ? -1f : 1f;

            // Center circle (Primary layer)
            args.WorldHandle.DrawCircle(worldPosition, GuideRadius, _guideColor);
            // Inner ring: Secondary and Tertiary
            args.WorldHandle.DrawCircle(worldPosition + gridRotation.RotateVec(new Vector2(multi * GuideOffset, GuideOffset)), GuideRadius, _guideColor);
            args.WorldHandle.DrawCircle(worldPosition - gridRotation.RotateVec(new Vector2(multi * GuideOffset, GuideOffset)), GuideRadius, _guideColor);

            // RPD supports 5 layers; RPLD only supports 3 layers.
            if (!rcd.IsRPLD)
            {
                // Outer ring: Quaternary and Quinary
                args.WorldHandle.DrawCircle(worldPosition + gridRotation.RotateVec(new Vector2(multi * GuideOffset * 2, GuideOffset * 2)), GuideRadius, _guideColor);
                args.WorldHandle.DrawCircle(worldPosition - gridRotation.RotateVec(new Vector2(multi * GuideOffset * 2, GuideOffset * 2)), GuideRadius, _guideColor);
            }
        }

        base.Render(args);
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        _mouseCoordsRaw = ScreenToCursorGrid(mouseScreen);
        MouseCoords = _mouseCoordsRaw.AlignWithClosestGridTile(SearchBoxSize, _entityManager, _mapManager);

        var gridId = _transformSystem.GetGrid(MouseCoords);

        if (!_entityManager.TryGetComponent<MapGridComponent>(gridId, out var mapGrid))
            return;

        CurrentTile = _mapSystem.GetTileRef(gridId.Value, mapGrid, MouseCoords);

        float tileSize = mapGrid.TileSize;
        GridDistancing = tileSize;

        if (pManager.CurrentPermission!.IsTile)
        {
            MouseCoords = new EntityCoordinates(MouseCoords.EntityId, new Vector2(CurrentTile.X + (tileSize / 2),
                CurrentTile.Y + (tileSize / 2)));
        }
        else
        {
            MouseCoords = new EntityCoordinates(MouseCoords.EntityId, new Vector2(CurrentTile.X + (tileSize / 2) + pManager.PlacementOffset.X,
                CurrentTile.Y + (tileSize / 2) + pManager.PlacementOffset.Y));
        }

        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null)
            return;

        if (!_handsSystem.TryGetActiveItem(player.Value, out var heldEntity))
            return;

        if (!_entityManager.TryGetComponent<RCDComponent>(heldEntity, out var rcd) || (!rcd.IsRpd && !rcd.IsRPLD))
            return;

        if (!_entityManager.TryGetComponent<TransformComponent>(player.Value, out var playerXform))
            return;

        if (!_transformSystem.InRange(playerXform.Coordinates, MouseCoords, SharedInteractionSystem.InteractionRange))
            return;

        var mouseCoordsDiff = _mouseCoordsRaw.Position - MouseCoords.Position;
        var newLayer = AtmosPipeLayer.Primary; // fallback

        // Get held RCD to check CurrentMode
        switch (rcd.CurrentMode)
        {
            case RpdMode.Primary:
                newLayer = AtmosPipeLayer.Primary;
                break;

            case RpdMode.Secondary:
                newLayer = AtmosPipeLayer.Secondary;
                break;

            case RpdMode.Tertiary:
                newLayer = AtmosPipeLayer.Tertiary;
                break;

            case RpdMode.Quaternary:
                newLayer = rcd.IsRPLD ? AtmosPipeLayer.Tertiary : AtmosPipeLayer.Quaternary;
                break;

            case RpdMode.Quinary:
                newLayer = rcd.IsRPLD ? AtmosPipeLayer.Tertiary : AtmosPipeLayer.Quinary;
                break;

            case RpdMode.Free:
                // Only in Free mode do we use mouse direction and distance
                if (mouseCoordsDiff.Length() > MouseDeadzoneRadius / 2)
                {
                    var gridRotation = _transformSystem.GetWorldRotation(gridId.Value);
                    var direction = (new Angle(mouseCoordsDiff) + _eyeManager.CurrentEye.Rotation + gridRotation + (Math.PI / 2)).GetCardinalDir();

                    // RPD uses 5-layer free placement (inner + outer rings), RPLD uses 3-layer (inner ring only).
                    if (!rcd.IsRPLD && mouseCoordsDiff.Length() > MouseDeadzoneRadius)
                    {
                        // Outer ring
                        newLayer = (direction is Direction.North or Direction.East) ? AtmosPipeLayer.Quaternary : AtmosPipeLayer.Quinary;
                    }
                    else
                    {
                        // Inner ring
                        newLayer = (direction is Direction.North or Direction.East) ? AtmosPipeLayer.Secondary : AtmosPipeLayer.Tertiary;
                    }
                }
                break;
        }

        // Update layer if changed
        if (newLayer != _currentLayer)
            _currentLayer = newLayer;

        if (rcd.CurrentMode == RpdMode.Free)
        {
            UpdateSelectedLayer(heldEntity.Value, _currentLayer);
        }

        UpdatePlacer(_currentLayer);
    }

    // Why this replaced UpdateEyeRotation:
    // - Free-mode preview computes an explicit layer choice on the client from cursor position.
    // - The old approach only synced camera/eye rotation and asked the server to recompute the layer.
    //
    // What this does instead:
    // - Whenever the locally selected free-mode layer changes (or held RPD/RPLD changes),
    //   sends that exact layer as RPDSelectedLayerEvent.
    // - Server stores it on the held RCDComponent (LastSelectedLayer)
    //   and uses it directly during placement in Free mode.
    private void UpdateSelectedLayer(EntityUid heldEntity, AtmosPipeLayer layer)
    {
        if (_lastLayerSyncEntity != heldEntity || _lastLayerSynced != layer)
        {
            _lastLayerSyncEntity = heldEntity;
            _lastLayerSynced = layer;
            _entityNetwork.SendSystemNetworkMessage(new RPDSelectedLayerEvent(_entityManager.GetNetEntity(heldEntity), (byte) layer));
        }
    }

    private void UpdatePlacer(AtmosPipeLayer layer)
    {
        // Try to get alternative prototypes from the entity atmos pipe layer component
        if (pManager.CurrentPermission?.EntityType == null)
            return;

        if (!_protoManager.TryIndex<EntityPrototype>(pManager.CurrentPermission.EntityType, out var currentProto))
            return;

        if (!currentProto.TryGetComponent<AtmosPipeLayersComponent>(out var atmosPipeLayers, _entityManager.ComponentFactory))
            return;

        if (!_pipeLayersSystem.TryGetAlternativePrototype(atmosPipeLayers, layer, out var newProtoId))
            return;

        if (_protoManager.TryIndex<EntityPrototype>(newProtoId, out var newProto))
        {
            // Update the placed prototype
            pManager.CurrentPermission.EntityType = newProtoId;

            // Update the appearance of the ghost sprite
            if (newProto.TryGetComponent<SpriteComponent>(out var sprite, _entityManager.ComponentFactory))
            {
                var textures = new List<IDirectionalTextureProvider>();

                foreach (var spriteLayer in sprite.AllLayers)
                {
                    if (spriteLayer.ActualRsi?.Path != null && spriteLayer.RsiState.Name != null)
                        textures.Add(_spriteSystem.RsiStateLike(new SpriteSpecifier.Rsi(spriteLayer.ActualRsi.Path, spriteLayer.RsiState.Name)));
                }

                pManager.CurrentTextures = textures;
            }
        }
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;

        // If the destination is out of interaction range, set the placer alpha to zero
        if (!_entityManager.TryGetComponent<TransformComponent>(player, out var xform))
            return false;

        if (!_transformSystem.InRange(xform.Coordinates, position, SharedInteractionSystem.InteractionRange))
        {
            InvalidPlaceColor = InvalidPlaceColor.WithAlpha(0);
            return false;
        }

        // Otherwise restore the alpha value
        else
        {
            InvalidPlaceColor = InvalidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
        }

        // Determine if player is carrying an RCD in their active hand
        if (!_handsSystem.TryGetActiveItem(player.Value, out var heldEntity))
            return false;

        if (!_entityManager.TryGetComponent<RCDComponent>(heldEntity, out var rcd))
            return false;

        var gridUid = _transformSystem.GetGrid(position);
        if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var mapGrid))
            return false;
        var tile = _mapSystem.GetTileRef(gridUid.Value, mapGrid, position);
        var posVector = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, position);

        // Determine if the user is hovering over a target
        var currentState = _stateManager.CurrentState;

        if (currentState is not GameplayStateBase screen)
            return false;

        var target = screen.GetClickedEntity(_transformSystem.ToMapCoordinates(_mouseCoordsRaw));

        // Determine if the RCD operation is valid or not
        if (!_rcdSystem.IsRCDOperationStillValid(heldEntity.Value, rcd, gridUid.Value, mapGrid, tile, posVector, target, player.Value, false))
            return false;

        return true;
    }
}
