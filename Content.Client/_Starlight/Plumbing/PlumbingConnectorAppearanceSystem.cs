using System.Numerics;
using Content.Shared._Starlight.Plumbing;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Client.SubFloor;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Plumbing;

/// <summary>
///     Client system that creates and updates plumbing connector sprite layers.
///     Single layer per direction that switches between disconnected/connected sprites
///     Layers hide when covered by floor tiles (server sends CoveredByFloor state)
/// </summary>
[UsedImplicitly]
public sealed class PlumbingConnectorAppearanceSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly Color InletColor = new(1.0f, 0.35f, 0.35f);  // Vibrant Red
    private static readonly Color OutletColor = new(0.35f, 0.6f, 1.0f);  // Vibrant Blue
    private static readonly Color MixingInletColor = new(0.35f, 0.9f, 0.35f);  // Vibrant Green
    private static readonly PlumbingConnectionLayer[] ConnectionLayers = Enum.GetValues<PlumbingConnectionLayer>();

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<PlumbingConnectorAppearanceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PlumbingConnectorAppearanceComponent, AppearanceChangeEvent>(OnAppearanceChanged, after: [typeof(SubFloorHideSystem)]);
    }

    private void OnInit(EntityUid uid, PlumbingConnectorAppearanceComponent component, ComponentInit args)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        // Create one layer for each cardinal direction
        // The layer will swap between disconnected/connected sprites based on connection state
        // Insert at layer 0 so connectors render UNDER the plumbing machine sprite
        foreach (var layerKey in ConnectionLayers)
        {
            var offset = GetDirectionOffset(layerKey, component.Offset);

            // Each insertion at 0 pushes previous layers up, so we use index 0 for all operations
            var layerName = layerKey.ToString();
            _sprite.AddBlankLayer((uid, sprite), 0);
            _sprite.LayerMapSet((uid, sprite), layerName, 0);

            // Disconnected connectors are offset from center to show under big machine sprites. 
            _sprite.LayerSetRsi((uid, sprite), 0, component.Disconnected.RsiPath);
            _sprite.LayerSetRsiState((uid, sprite), 0, component.Disconnected.RsiState);
            _sprite.LayerSetDirOffset((uid, sprite), 0, ToOffset(layerKey));
            _sprite.LayerSetVisible((uid, sprite), 0, false);
            if (offset != Vector2.Zero)
                _sprite.LayerSetOffset((uid, sprite), 0, offset);
        }
    }

    private static Vector2 GetDirectionOffset(PlumbingConnectionLayer layer, float offset)
    {
        return ((PipeDirection)layer).ToDirection().ToVec() * offset;
    }

    private void OnAppearanceChanged(EntityUid uid, PlumbingConnectorAppearanceComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.Sprite.Visible)
        {
            return;
        }

        // Hide if no nodes exists somehow
        if (!_appearance.TryGetData<int>(uid, PlumbingVisuals.NodeDirections, out var nodeDirectionsInt, args.Component))
        {
            HideAllLayers(uid, args.Sprite);
            return;
        }

        if (!_appearance.TryGetData<int>(uid, PlumbingVisuals.ConnectedDirections, out var connectedDirectionsInt, args.Component))
            connectedDirectionsInt = 0;

        if (!_appearance.TryGetData<int>(uid, PlumbingVisuals.InletDirections, out var inletDirectionsInt, args.Component))
            inletDirectionsInt = 0;
        if (!_appearance.TryGetData<int>(uid, PlumbingVisuals.OutletDirections, out var outletDirectionsInt, args.Component))
            outletDirectionsInt = 0;
        if (!_appearance.TryGetData<int>(uid, PlumbingVisuals.MixingInletDirections, out var mixingInletDirectionsInt, args.Component))
            mixingInletDirectionsInt = 0;

        if (!_appearance.TryGetData<bool>(uid, PlumbingVisuals.CoveredByFloor, out var coveredByFloor, args.Component))
            coveredByFloor = false;

        var nodeDirections = (PipeDirection)nodeDirectionsInt;
        var connectedDirections = (PipeDirection)connectedDirectionsInt;
        var inletDirections = (PipeDirection)inletDirectionsInt;
        var outletDirections = (PipeDirection)outletDirectionsInt;
        var mixingInletDirections = (PipeDirection)mixingInletDirectionsInt;

        // Get the entity's local rotation to transform world directions to local
        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;
        var localRotation = xform.LocalRotation;
        var connectedDirectionsLocal = connectedDirections.RotatePipeDirection(-localRotation);
        var nodeDirectionsLocal = nodeDirections.RotatePipeDirection(-localRotation);
        var inletDirectionsLocal = inletDirections.RotatePipeDirection(-localRotation);
        var outletDirectionsLocal = outletDirections.RotatePipeDirection(-localRotation);
        var mixingInletDirectionsLocal = mixingInletDirections.RotatePipeDirection(-localRotation);


        foreach (var layerKey in ConnectionLayers)
        {
            var dir = (PipeDirection)layerKey;
            var hasNode = nodeDirectionsLocal.HasDirection(dir);
            var isConnected = connectedDirectionsLocal.HasDirection(dir);
            var isInlet = inletDirectionsLocal.HasDirection(dir);
            var isOutlet = outletDirectionsLocal.HasDirection(dir);
            var isMixingInlet = mixingInletDirectionsLocal.HasDirection(dir);

            // Determine color based on inlet/outlet/mixing
            var color = isMixingInlet ? MixingInletColor : isInlet ? InletColor : isOutlet ? OutletColor : Color.White;

            var layerName = layerKey.ToString();
            if (_sprite.LayerMapTryGet((uid, args.Sprite), layerName, out var layerKey2, false))
            {
                var layer = args.Sprite[layerKey2];
                layer.Visible = hasNode && !coveredByFloor;
                
                if (layer.Visible)
                {
                    // Swap sprite based on connection state
                    if (isConnected)
                    {
                        _sprite.LayerSetRsiState((uid, args.Sprite), layerKey2, component.Connected.RsiState);
                        _sprite.LayerSetOffset((uid, args.Sprite), layerKey2, Vector2.Zero);
                    }
                    else
                    {
                        _sprite.LayerSetRsiState((uid, args.Sprite), layerKey2, component.Disconnected.RsiState);
                        _sprite.LayerSetOffset((uid, args.Sprite), layerKey2, GetDirectionOffset(layerKey, component.Offset));
                    }
                    layer.Color = color;
                }
            }
        }
    }

    private void HideAllLayers(EntityUid uid, SpriteComponent sprite)
    {
        foreach (var layerKey in ConnectionLayers)
        {
            var layerName = layerKey.ToString();
            if (_sprite.LayerMapTryGet((uid, sprite), layerName, out var key, false))
                sprite[key].Visible = false;
        }
    }


    private SpriteComponent.DirectionOffset ToOffset(PlumbingConnectionLayer layer)
    {
        return layer switch
        {
            PlumbingConnectionLayer.NorthConnection => SpriteComponent.DirectionOffset.Flip,
            PlumbingConnectionLayer.EastConnection => SpriteComponent.DirectionOffset.CounterClockwise,
            PlumbingConnectionLayer.WestConnection => SpriteComponent.DirectionOffset.Clockwise,
            _ => SpriteComponent.DirectionOffset.None, // SouthConnection
        };
    }

    private enum PlumbingConnectionLayer : byte
    {
        NorthConnection = PipeDirection.North,
        SouthConnection = PipeDirection.South,
        EastConnection = PipeDirection.East,
        WestConnection = PipeDirection.West,
    }
}
