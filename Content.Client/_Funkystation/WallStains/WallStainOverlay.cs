using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Light;
using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Funkystation.WallStains;

public sealed partial class WallStainOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";

    private static readonly ProtoId<TagPrototype> DirectionalWindowTag = "DirectionalWindow";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly ProtoId<TagPrototype> AirlockTag = "Airlock";

    [Dependency] private IClyde _clyde = null!;
    [Dependency] private IEntityManager _entityManager = null!;
    [Dependency] private IPrototypeManager _prototypeManager = null!;
    [Dependency] private IGameTiming _gameTiming = null!;
    [Dependency] public IMapManager MapManager = null!;

    private readonly TransformSystem _transformSystem;
    private readonly SpriteSystem _spriteSystem;
    private readonly EntityLookupSystem _entityLookupSystem;
    private readonly TagSystem _tagSystem;

    private readonly EntityQuery<TransformComponent> _transformQuery;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly HashSet<Entity<WallStainComponent>> _visibleStains = [];
    private readonly HashSet<EntityUid> _tempEntities = [];
    private readonly HashSet<EntityUid> _intersectingEntities = [];
    private readonly OverlayResourceCache<CachedResources> _resources = new();

    private const float DblPixelsPerMeter = 2f * EyeManager.PixelsPerMeter;

    public WallStainOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transformSystem = _entityManager.System<TransformSystem>();
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _entityLookupSystem = _entityManager.System<EntityLookupSystem>();
        _tagSystem = _entityManager.System<TagSystem>();

        _transformQuery = _entityManager.GetEntityQuery<TransformComponent>();

        ZIndex = AfterLightTargetOverlay.ContentZIndex + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var mapId = args.MapId;
        var worldBounds = args.WorldBounds;
        var worldHandle = args.WorldHandle;
        var target = viewport.RenderTarget;
        var invMatrix = viewport.GetWorldToLocalMatrix();
        var realTime = _gameTiming.RealTime;

        _visibleStains.Clear();
        _entityLookupSystem.GetEntitiesIntersecting(mapId, worldBounds, _visibleStains);

        if (_visibleStains.Count == 0)
            return;

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());

        if (res.StainTarget?.Texture.Size != target.Size)
        {
            res.StainTarget?.Dispose();
            res.StainTarget = _clyde.CreateRenderTarget(target.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "stain-stencil-target");
        }

        args.WorldHandle.RenderInRenderTarget(res.StainTarget,
            () =>
            {
                _intersectingEntities.Clear();
                _tempEntities.Clear();

                worldHandle.UseShader(_prototypeManager.Index(UnshadedShader).Instance());

                foreach (var stainEntity in _visibleStains)
                {
                    if (!_transformQuery.TryGetComponent(stainEntity.Owner, out var stainXform))
                        continue;

                    var stainWorldPos = _transformSystem.GetWorldPosition(stainXform);
                    var queryBox = Box2.CenteredAround(stainWorldPos, new Vector2(3.0f, 3.0f));

                    _entityLookupSystem.GetEntitiesIntersecting(mapId, queryBox, _tempEntities, LookupFlags.Static);

                    foreach (var uid in _tempEntities)
                    {
                        // We only want to draw stencil masks on entities that have a Sprite and Transform
                        if (!_transformQuery.TryGetComponent(uid, out var transformComponent) ||
                            !_entityManager.TryGetComponent<SpriteComponent>(uid, out _))
                            continue;

                        // Andddd only draw stains onto anchored entities
                        if (!transformComponent.Anchored)
                            continue;

                        // AAAAAAAAAAND directional windows don't cover the full tile, so skip them to avoid floating stains
                        if (_tagSystem.HasTag(uid, DirectionalWindowTag))
                            continue;

                        // Finally, make sure the entity is one of the following:
                        if (!_tagSystem.HasTag(uid, WallTag) &&
                            !_tagSystem.HasTag(uid, WindowTag) &&
                            !_tagSystem.HasTag(uid, AirlockTag))
                        {
                            continue;
                        }

                        _intersectingEntities.Add(uid);
                    }
                    _tempEntities.Clear();
                }

                foreach (var uid in _intersectingEntities)
                {
                    if (!_transformQuery.TryGetComponent(uid, out var transformComponent) ||
                        !_entityManager.TryGetComponent<SpriteComponent>(uid, out var spriteComponent))
                        continue;

                    if (transformComponent.GridUid == null)
                        continue;

                    var gridUid = transformComponent.GridUid.Value;
                    var localMatrix = Matrix3x2.Multiply(_transformSystem.GetWorldMatrix(gridUid, _transformQuery), invMatrix);
                    worldHandle.SetTransform(localMatrix);

                    var bounds = _spriteSystem.CalculateBounds((uid, spriteComponent), transformComponent.Coordinates.Position, transformComponent.LocalRotation, viewport.Eye?.Rotation ?? Angle.Zero);
                    worldHandle.DrawRect(bounds, Color.White);
                }

            },
            Color.Transparent);

        worldHandle.SetTransform(Matrix3x2.Identity);

        worldHandle.UseShader(_prototypeManager.Index(StencilMaskShader).Instance());
        worldHandle.DrawTextureRect(res.StainTarget.Texture, worldBounds);

        worldHandle.UseShader(_prototypeManager.Index(StencilEqualDrawShader).Instance());

        foreach (var stainEntity in _visibleStains)
        {
            var uid = stainEntity.Owner;
            var stain = stainEntity.Comp;

            if (!_transformQuery.TryGetComponent(uid, out var xform))
                continue;

            var state = string.IsNullOrEmpty(stain.StainState) ? "splatter" : stain.StainState;
            var rsiSpec = new SpriteSpecifier.Rsi(new ResPath("/Textures/Effects/crayondecals.rsi"), state);

            Texture? texture;
            try
            {
                texture = _spriteSystem.GetFrame(rsiSpec, realTime);
            }
            catch (Exception)
            {
                try
                {
                    var fallbackSpec = new SpriteSpecifier.Rsi(new ResPath("/Textures/Effects/crayondecals.rsi"), "splatter");
                    texture = _spriteSystem.GetFrame(fallbackSpec, realTime);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            var convertedTextureWidth = texture.Width / DblPixelsPerMeter;
            var convertedTextureHeight = texture.Height / DblPixelsPerMeter;

            var (_, _, worldMatrix) = _transformSystem.GetWorldPositionRotationMatrix(xform);
            worldHandle.SetTransform(worldMatrix);

            var scaleX = 1.0f;
            var scaleY = 1.0f;

            if (stain.Direction.Y != 0)
            {
                scaleX = 2.2f;
            }
            else if (stain.Direction.X != 0)
            {
                scaleY = 2.2f;
            }

            var rect = new Box2(-convertedTextureWidth * scaleX, -convertedTextureHeight * scaleY, convertedTextureWidth * scaleX, convertedTextureHeight * scaleY);

            worldHandle.DrawTextureRect(
                texture,
                rect,
                modulate: stain.Color
            );
        }

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
        public IRenderTexture? StainTarget;

        public void Dispose()
        {
            StainTarget?.Dispose();
        }
    }
}
