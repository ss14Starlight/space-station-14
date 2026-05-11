using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Client.Graphics;
using Content.Shared.Pinpointer;
using Content.Shared.Silicons.StationAi;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Client._Starlight.Silicons.StationAi;

namespace Content.Client.Silicons.StationAi;

public sealed class StationAiOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CameraStaticShader = "CameraStatic";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    // Starlight start
    private static readonly ProtoId<ShaderPrototype> StencilDrawShader = "StencilDrawUnshaded";
    // Starlight end

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities; // Starlight

    private readonly HashSet<Vector2i> _visibleTiles = new();
    private readonly Dictionary<Vector2i, HashSet<string>> _visibleTileTags = []; // Starlight

    private readonly OverlayResourceCache<CachedResources> _resources = new();

    private static readonly RenderTargetFormatParameters _renderParams = new(RenderTargetColorFormat.Rgba8Srgb); // Carpmosia
    private const float UpdateRate = 1f / 30f; // Carpmosia

    private float _accumulator;

    // Starlight start
    private readonly CyberspaceNavMapRenderer _cyberspaceRenderer;
    private EntityUid _lastGridUid = EntityUid.Invalid;
    // Starlight end

    public StationAiOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.CyberspaceOverlays; // Starlight: above all normal DrawDepths (max=13), below CyberspaceObjects (100)
        _cyberspaceRenderer = new CyberspaceNavMapRenderer(_proto); // Starlight
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (res.StencilTexture?.Texture.Size != args.Viewport.Size)
        {
            res.StaticTexture?.Dispose();
            res.StencilTexture?.Dispose();

            // Carpmosia-start - AI Navmap
            res.StencilTexture = _clyde.CreateRenderTarget(args.Viewport.Size, _renderParams, name: "station-ai-stencil");
            res.StaticTexture = _clyde.CreateRenderTarget(args.Viewport.Size, _renderParams, name: "station-ai-static");
            // Carpmosia-end - AI Navmap
        }

        var worldHandle = args.WorldHandle;

        var worldBounds = args.WorldBounds;

        // Starlight-start: moved to be after new playerEnt definition with edit
        var playerEnt = _player.LocalEntity;

        // Check for cross-grid viewing (e.g., Abductor remote eye) BEFORE getting gridUid
        if (_entManager.TryGetComponent(playerEnt, out StationAiOverlayComponent? stationAiOverlay)
            && stationAiOverlay.AllowCrossGrid
            && _entManager.TryGetComponent(playerEnt, out RelayInputMoverComponent? relay))
            playerEnt = relay.RelayEntity;

        _entManager.TryGetComponent(playerEnt, out StationAiOverlayComponent? relayStationAiOverlay);
        _entManager.TryGetComponent(playerEnt, out TransformComponent? playerXform);

        var gridUid = playerXform?.GridUid
            ?? (stationAiOverlay is { AllowCrossGrid: true } ? _lastGridUid : EntityUid.Invalid);
        if (gridUid != EntityUid.Invalid)
            _lastGridUid = gridUid;

        _entManager.TryGetComponent(gridUid, out MapGridComponent? grid);
        _entManager.TryGetComponent(gridUid, out BroadphaseComponent? broadphase);
        _entManager.TryGetComponent(gridUid, out NavMapComponent? navMap);
        // Starlight-end

        var invMatrix = args.Viewport.GetWorldToLocalMatrix();
        _accumulator -= (float)_timing.FrameTime.TotalSeconds;

        if (grid != null && broadphase != null)
        {
            var lookups = _entManager.System<EntityLookupSystem>();
            var xforms = _entManager.System<SharedTransformSystem>();

            var color = Color.White; // 🌟Starlight🌟
            if (stationAiOverlay is not null) // 🌟Starlight🌟
                color = color.WithAlpha(stationAiOverlay.Alfa); // 🌟Starlight🌟

            // Starlight: rebuild cached navmap geometry on timer, grid change, or camera move
            _cyberspaceRenderer.Update((float)_timing.FrameTime.TotalSeconds, gridUid, navMap, grid, xforms, worldBounds);
            if (_accumulator <= 0f)
            {
                _accumulator = MathF.Max(0f, _accumulator + UpdateRate); // Carpmosia-edit - AI Navmap
                _visibleTiles.Clear();
                // Starlight - start
                _visibleTileTags.Clear();
                _entManager.System<StationAiVisionSystem>().GetView((gridUid, broadphase, grid), worldBounds, _visibleTiles, _visibleTileTags);
                // Starlight - end
            }

            var gridMatrix = xforms.GetWorldMatrix(gridUid);
            var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

            // Draw visible tiles to stencil
            worldHandle.RenderInRenderTarget(res.StencilTexture!, () =>
            {
                worldHandle.SetTransform(matty);

                foreach (var tile in _visibleTiles)
                {
                    // Starlight-start: Only render tiles that have all required render tags
                    var allTagsPresent = true;
                    if (relayStationAiOverlay is not null)
                    {
                        foreach (var requiredTag in relayStationAiOverlay.RequiredTags)
                        {
                            if (_visibleTileTags.TryGetValue(tile, out var tag))
                                if (!tag.Contains(requiredTag))
                                {
                                    allTagsPresent = false;
                                    break;
                                }
                        }
                    }

                    if (allTagsPresent)
                    {
                        var aabb = lookups.GetLocalBounds(tile, grid.TileSize);
                        worldHandle.DrawRect(aabb, Color.White);
                    }
                    // Starlight-end
                }
            },
            Color.Transparent);

            // Once this is gucci optimise rendering.
            worldHandle.RenderInRenderTarget(res.StaticTexture!,
                () => _cyberspaceRenderer.Draw(worldHandle, matty), // Starlight
            Color.Black);
        }
        // Not on a grid
        else
        {
            worldHandle.RenderInRenderTarget(res.StencilTexture!, () =>
            {
            },
            Color.Transparent);

            worldHandle.RenderInRenderTarget(res.StaticTexture!,
            () =>
            {
                worldHandle.SetTransform(Matrix3x2.Identity);
                worldHandle.DrawRect(worldBounds, Color.Black);
            }, Color.Black);
        }

        // Use the lighting as a mask
        worldHandle.UseShader(_proto.Index(StencilMaskShader).Instance());
        worldHandle.DrawTextureRect(res.StencilTexture!.Texture, worldBounds);

        // Draw the static
        worldHandle.UseShader(_proto.Index(StencilDrawShader).Instance());
        worldHandle.DrawTextureRect(res.StaticTexture!.Texture, worldBounds);

        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(null);

    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? StaticTexture;
        public IRenderTexture? StencilTexture;

        public void Dispose()
        {
            StaticTexture?.Dispose();
            StencilTexture?.Dispose();
        }
    }
    }
