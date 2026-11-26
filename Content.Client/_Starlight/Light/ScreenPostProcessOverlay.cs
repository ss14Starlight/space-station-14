using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Client.Graphics;
using Content.Client.Light.EntitySystems;
using Content.Client.Parallax.Data;
using Content.Shared._Starlight.Light;
using Content.Shared.Parallax;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

public sealed partial class ScreenPostProcessOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _shader = "ScreenPostProcess";
    private static readonly ProtoId<ShaderPrototype> _bloomShader = "BloomExtract";
    private static readonly ProtoId<ShaderPrototype> _emissionShader = "GiEmissionExtract";
    private static readonly ProtoId<ShaderPrototype> _debugCopyShader = "GiCopy";
    private static readonly ProtoId<ShaderPrototype> _maskShader = "GiClear";
    private static readonly ProtoId<ShaderPrototype> _albedoShader = "GiAlbedoExtract";

    /// <summary>
    /// Resolution of the emitter texture handed to <see cref="GlobalIlluminationOverlay"/>, relative to the viewport.
    /// </summary>
    private const float EmissionScale = 0.25f;

    /// <summary>
    /// Extra tiles masked around moving sprites, for outlines and the reprojection being a frame late.
    /// </summary>
    private const float MovingSpriteMargin = 0.25f;

    private const float BloomScale = 0.5f;

    /// <summary>
    /// Linear filtering for the reduced resolution targets, so they don't show steps when read at screen size.
    /// </summary>
    private static readonly TextureSampleParameters _smoothSampling = new() { Filter = true };

    /// <summary>
    /// Color of the light open space throws through windows, before its strength.
    /// </summary>
    private static readonly Color _defaultStarlightColor = Color.FromHex("#9db8ff");

    public const float MinBrightness = 0.8f;
    public const float MaxBrightness = 1.3f;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public const int ContentZIndex = -130;

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly ShaderInstance _shaderInst;
    private readonly ShaderInstance _bloomShaderInst;
    private readonly ShaderInstance _emissionShaderInst;
    private readonly ShaderInstance _debugCopyShaderInst;
    private readonly GridStencilSystem _gridStencil;
    private readonly EntityLookupSystem _lookup;
    private readonly ColorGradingSystem _grading;
    private readonly ScreenImpactSystem _impact;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _xform;
    private readonly SharedMapSystem _map;
    private readonly ShaderInstance _maskShaderInst;
    private readonly HashSet<Entity<SpriteComponent>> _movingSprites = [];
    private readonly List<Box2> _movingSpriteBounds = [];
    private readonly ShaderInstance _albedoShaderInst;
    private readonly GridStencilSystem.TileStencilPredicate _isStationTile;

    private readonly CVarSubscriptions _cvars;
    private float _brightness;
    private float _bloomThreshold;
    private float _bloomRadius;

    // Strengths passed to the shader. Each one is 0 while its effect is turned off.
    private float _nightVision, _bloom, _vignette, _grain, _dust, _haze, _gloss;
    private bool _nightVisionOn, _bloomOn, _vignetteOn, _grainOn, _dustOn, _hazeOn, _glossOn;
    private float _nightVisionStrength, _bloomStrength, _vignetteStrength, _grainStrength, _dustStrength, _hazeStrength, _glossStrength;

    private bool _giOn;
    private bool _emissionOn;
    private float _emissionThreshold;
    private float _emissionStrength;
    private bool _starlightOn;
    private float _starlightStrength;
    private bool _surfaceOn;
    private int _giDebug;

    public ScreenPostProcessOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;

        _shaderInst = _proto.Index(_shader).InstanceUnique();
        _bloomShaderInst = _proto.Index(_bloomShader).InstanceUnique();
        _emissionShaderInst = _proto.Index(_emissionShader).InstanceUnique();
        _debugCopyShaderInst = _proto.Index(_debugCopyShader).InstanceUnique();
        _gridStencil = _entMan.System<GridStencilSystem>();
        _lookup = _entMan.System<EntityLookupSystem>();
        _grading = _entMan.System<ColorGradingSystem>();
        _impact = _entMan.System<ScreenImpactSystem>();
        _sprite = _entMan.System<SpriteSystem>();
        _xform = _entMan.System<SharedTransformSystem>();
        _map = _entMan.System<SharedMapSystem>();
        _maskShaderInst = _proto.Index(_maskShader).InstanceUnique();
        _albedoShaderInst = _proto.Index(_albedoShader).InstanceUnique();
        _isStationTile = IsStationTile;

        _cvars = new CVarSubscriptions(_cfg);
        _cvars.Track(StarlightCCVars.ScreenBrightness, v => _brightness = Math.Clamp(v, MinBrightness, MaxBrightness));
        _cvars.Track(StarlightCCVars.NightVision, v => { _nightVisionOn = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.NightVisionStrength, v => { _nightVisionStrength = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.Bloom, v => { _bloomOn = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.BloomStrength, v => { _bloomStrength = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.Vignette, v => { _vignetteOn = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.VignetteStrength, v => { _vignetteStrength = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.FilmGrain, v => { _grainOn = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.FilmGrainStrength, v => { _grainStrength = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.DustMotes, v => { _dustOn = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.DustMotesStrength, v => { _dustStrength = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.HeatHaze, v => { _hazeOn = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.HeatHazeStrength, v => { _hazeStrength = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.FloorGloss, v => { _glossOn = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.FloorGlossStrength, v => { _glossStrength = v; UpdateStrengths(); });
        _cvars.Track(StarlightCCVars.BloomThreshold, v => _bloomThreshold = Math.Clamp(v, 0f, 1f));
        _cvars.Track(StarlightCCVars.BloomRadius, v => _bloomRadius = MathF.Max(0f, v));
        _cvars.Track(StarlightCCVars.GlobalIllumination, v => _giOn = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationEmission, v => _emissionOn = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationEmissionThreshold, v => _emissionThreshold = Math.Clamp(v, 0f, 1f));
        _cvars.Track(StarlightCCVars.GlobalIlluminationEmissionStrength, v => _emissionStrength = MathF.Max(0f, v));
        _cvars.Track(StarlightCCVars.GlobalIlluminationStarlight, v => _starlightOn = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationStarlightStrength, v => _starlightStrength = MathF.Max(0f, v));
        _cvars.Track(StarlightCCVars.GlobalIlluminationSurfaceColor, v => _surfaceOn = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationDebug, v => _giDebug = v);
    }

    private void UpdateStrengths()
    {
        _nightVision = _nightVisionOn ? Math.Clamp(_nightVisionStrength, 0f, 1f) : 0f;
        _bloom = _bloomOn ? Math.Clamp(_bloomStrength, 0f, 3f) : 0f;
        _vignette = _vignetteOn ? Math.Clamp(_vignetteStrength, 0f, 1f) : 0f;
        _grain = _grainOn ? Math.Clamp(_grainStrength, 0f, 1f) : 0f;
        _dust = _dustOn ? Math.Clamp(_dustStrength, 0f, 1f) : 0f;
        _haze = _hazeOn ? Math.Clamp(_hazeStrength, 0f, 1f) : 0f;
        _gloss = _glossOn ? Math.Clamp(_glossStrength, 0f, 1f) : 0f;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity == null || args.Viewport.Eye == null || args.Viewport.Eye != _eye.CurrentEye)
            return false;

        return EmissionWanted() || ScreenEffectsWanted();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var eye = args.Viewport.Eye!;
        var lit = eye.DrawLight
                && _lightManager.Enabled
                && _lightManager.DrawLighting
                && _entMan.TryGetComponent<MapComponent>(args.MapUid, out var map)
                && map.LightingEnabled;

        // Without the light map there is no telling what glows.
        var bloom = lit ? _bloom : 0f;
        var bloomTexture = bloom > 0f || (lit && _gloss > 0f) ? DrawBloom(args) : null;

        if (lit && EmissionWanted())
            DrawEmission(args);

        if (!ScreenEffectsWanted())
        {
            DrawEmissionDebug(args);
            return;
        }

        _shaderInst.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shaderInst.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
        _shaderInst.SetParameter("BLOOM_TEXTURE", bloomTexture ?? ScreenTexture);
        var heatTexture = _haze > 0f ? DrawHeat(args) : null;
        _shaderInst.SetParameter("HEAT_TEXTURE", heatTexture ?? ScreenTexture);
        _shaderInst.SetParameter("haze", heatTexture != null ? _haze : 0f);
        var wetTexture = lit && _gloss > 0f ? DrawWet(args) : null;
        _shaderInst.SetParameter("WET_TEXTURE", wetTexture ?? ScreenTexture);
        _shaderInst.SetParameter("gloss", wetTexture != null ? _gloss : 0f);
        _shaderInst.SetParameter("glossGlow", bloomTexture != null ? 1f : 0f);
        _shaderInst.SetParameter("lightAvailable", lit ? 1f : 0f);
        _shaderInst.SetParameter("nightVision", _nightVision);
        _shaderInst.SetParameter("bloom", bloomTexture != null ? bloom : 0f);
        _shaderInst.SetParameter("vignette", _vignette);
        _shaderInst.SetParameter("brightness", _brightness);
        var grade = _grading.Current;
        _shaderInst.SetParameter("gradeTint", grade.Tint);
        _shaderInst.SetParameter("gradeShadows", grade.Shadows);
        _shaderInst.SetParameter("gradeSaturation", grade.Saturation);
        _shaderInst.SetParameter("gradeContrast", grade.Contrast);
        _shaderInst.SetParameter("grain", _grain);
        _shaderInst.SetParameter("aberration", _impact.Aberration);
        _shaderInst.SetParameter("flash", _impact.Flash);
        _shaderInst.SetParameter("flashColor", _impact.FlashColor);
        _shaderInst.SetParameter("dust", lit ? _dust : 0f);
        args.WorldBounds.GetCorners(out var bottomLeft, out var bottomRight, out _, out var topLeft);
        _shaderInst.SetParameter("worldOrigin", bottomLeft);
        _shaderInst.SetParameter("worldAxisX", bottomRight - bottomLeft);
        _shaderInst.SetParameter("worldAxisY", topLeft - bottomLeft);

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_shaderInst);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);

        DrawEmissionDebug(args);
    }

    /// <summary>
    /// Shows the glowing pixels (<c>lighting.gi_debug</c> 3), surface colours (4) or the station tile mask (5) handed
    /// to the bounce light. It is drawn on screen, not into the light map, so it does not change what is found next frame.
    /// </summary>
    private void DrawEmissionDebug(in OverlayDrawArgs args)
    {
        if (_giDebug is not (3 or 4 or 5))
            return;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        if (res.EmissionFrame != _timing.CurFrame)
            return;

        var texture = _giDebug switch
        {
            3 => res.Emission,
            4 => res.HasAlbedo ? res.Albedo : null,
            _ => res.Stencil,
        };
        if (texture == null)
            return;

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_debugCopyShaderInst);
        handle.DrawTextureRect(texture.Texture, args.WorldBounds);
        handle.UseShader(null);
    }

    /// <summary>
    /// The glowing pixels of the last frame this viewport drew, for <see cref="GlobalIlluminationOverlay"/>.
    /// The surface colours of that frame come with them when they were asked for.
    /// </summary>
    /// <param name="bounds">World area the textures cover, to reproject them into the next frame.</param>
    public bool TryGetEmission(IClydeViewport viewport,
        MapId map,
        [NotNullWhen(true)] out Texture? texture,
        out Texture? albedo,
        out Box2Rotated bounds)
    {
        texture = null;
        albedo = null;
        bounds = default;
        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Emission == null
            || res.EmissionFrame != _timing.CurFrame - 1
            || res.EmissionMap != map)
        {
            return false;
        }

        texture = res.Emission.Texture;
        albedo = res.HasAlbedo ? res.Albedo?.Texture : null;
        bounds = res.EmissionBounds;
        return true;
    }

    private bool EmissionWanted()
        => _giOn && (_emissionOn || _starlightOn || _surfaceOn);

    private bool ScreenEffectsWanted()
        => !_grading.Current.IsIdentity
            || _impact.Aberration > 0f
            || _impact.Flash > 0f
            || MathF.Abs(_brightness - 1f) > 0.001f
            || _nightVisionOn
            || _bloomOn
            || _vignetteOn
            || _grainOn
            || _dustOn
            || _hazeOn
            || _glossOn;

    private void DrawEmission(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.X * EmissionScale)),
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.Y * EmissionScale)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Emission?.Size != size)
        {
            if (res.FailedEmissionSize == size)
                return;

            res.Emission?.Dispose();
            res.Emission = null;
            try
            {
                res.Emission = _clyde.CreateRenderTarget(size,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F),
                    _smoothSampling,
                    name: "gi-emission");
            }
            catch (Exception e)
            {
                // No float targets, the bounce light is off as well then.
                res.FailedEmissionSize = size;
                _log.GetSawmill("light").Warning($"Glow capture disabled, can't create a float render target: {e.Message}");
                return;
            }
        }

        var target = res.Emission;
        var handle = args.WorldHandle;
        var matrix = target.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * (size / (Vector2) viewport.Size));
        var bounds = args.WorldBounds.Enlarged(1f);
        var stencil = _gridStencil.GetTileStencil(args, "gi-station", "gi-station-stencil", _isStationTile);

        _emissionShaderInst.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        _emissionShaderInst.SetParameter("LIGHT_TEXTURE", viewport.LightRenderTarget.Texture);
        _emissionShaderInst.SetParameter("STENCIL_TEXTURE", stencil.Texture);
        _emissionShaderInst.SetParameter("targetSize", (Vector2) size);
        _maskShaderInst.SetParameter("STENCIL_TEXTURE", stencil.Texture);
        _maskShaderInst.SetParameter("targetSize", (Vector2) size);
        _emissionShaderInst.SetParameter("threshold", _emissionThreshold);
        _emissionShaderInst.SetParameter("emissionStrength", _emissionOn ? _emissionStrength : 0f);
        var starlightColor = Color.FromSrgb(GetStarlightColor(args.MapUid));
        var starlight = new Vector3(starlightColor.R, starlightColor.G, starlightColor.B)
            * (_starlightOn ? _starlightStrength : 0f);
        _emissionShaderInst.SetParameter("starlight", starlight);

        // Only anchored things feed the bounce light. Anything that moves (ghosts and their outline, mobs,
        // carried items) would leave a trail behind in the accumulated bounce light.
        _movingSprites.Clear();
        _movingSpriteBounds.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB, _movingSprites,
            LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate);
        var eyeRotation = viewport.Eye!.Rotation;
        foreach (var sprite in _movingSprites)
        {
            // Anchored things without physics, such as catwalks, cables and wall lamps, land in the same lookup but
            // never move.
            if (!sprite.Comp.Visible || _entMan.GetComponent<TransformComponent>(sprite.Owner).Anchored)
                continue;

            var (position, rotation) = _xform.GetWorldPositionRotation(sprite.Owner);
            var spriteBounds = _sprite.CalculateBounds(sprite, position, rotation, eyeRotation);
            _movingSpriteBounds.Add(spriteBounds.CalcBoundingBox().Enlarged(MovingSpriteMargin));
        }

        _movingSprites.Clear();

        handle.RenderInRenderTarget(target,
            (Action)(() =>
            {
                handle.UseShader(_emissionShaderInst);
                handle.SetTransform(matrix);
                handle.DrawRect(bounds, Color.White);
                CutMovingSprites(handle);
            }), Color.Black);

        res.HasAlbedo = _surfaceOn && DrawAlbedo(res, size, matrix, bounds, handle, viewport, stencil);
        _movingSpriteBounds.Clear();

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);

        res.Stencil = stencil;
        res.EmissionFrame = _timing.CurFrame;
        res.EmissionBounds = args.WorldBounds;
        res.EmissionMap = args.MapId;
    }

    /// <summary>
    /// Writes the surface colours of this frame into <see cref="CachedResources.Albedo"/>. Returns false when the
    /// target can't be made.
    /// </summary>
    private bool DrawAlbedo(CachedResources res,
        Vector2i size,
        Matrix3x2 matrix,
        Box2Rotated bounds,
        DrawingHandleWorld handle,
        IClydeViewport viewport,
        IRenderTexture stencil)
    {
        if (res.Albedo?.Size != size)
        {
            if (res.FailedAlbedoSize == size)
                return false;

            res.Albedo?.Dispose();
            res.Albedo = null;
            try
            {
                res.Albedo = _clyde.CreateRenderTarget(size,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F),
                    _smoothSampling,
                    name: "gi-albedo");
            }
            catch (Exception e)
            {
                res.FailedAlbedoSize = size;
                _log.GetSawmill("light").Warning($"Surface colour capture disabled, can't create a float render target: {e.Message}");
                return false;
            }
        }

        _albedoShaderInst.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        _albedoShaderInst.SetParameter("LIGHT_TEXTURE", viewport.LightRenderTarget.Texture);
        _albedoShaderInst.SetParameter("STENCIL_TEXTURE", stencil.Texture);
        _albedoShaderInst.SetParameter("targetSize", (Vector2) size);

        handle.RenderInRenderTarget(res.Albedo,
            (Action)(() =>
            {
                handle.UseShader(_albedoShaderInst);
                handle.SetTransform(matrix);
                handle.DrawRect(bounds, Color.White);
                CutMovingSprites(handle);
            }), Color.Transparent);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        return true;
    }

    /// <summary>
    /// Whether a tile counts as station for the bounce light. Lattice does, so catwalks over it are lit by the stars
    /// around them. Floors that show space, such as glass, count as open space and give off starlight.
    /// </summary>
    private bool IsStationTile(Entity<MapGridComponent> grid, TileRef tile)
    {
        if (tile.Tile.IsEmpty)
            return false;

        return _tileDefs[tile.Tile.TypeId] is not ContentTileDefinition { ShowsSpace: true };
    }

    private Color GetStarlightColor(EntityUid map)
    {
        if (_entMan.TryGetComponent<StarlightColorComponent>(map, out var starlight))
            return starlight.Color;

        if (_entMan.TryGetComponent<ParallaxComponent>(map, out var parallax)
            && _proto.TryIndex<ParallaxPrototype>(parallax.Parallax, out var proto)
            && proto.StarlightColor is { } color)
            return color;

        return _defaultStarlightColor;
    }

    private void CutMovingSprites(DrawingHandleWorld handle)
    {
        handle.UseShader(_maskShaderInst);
        foreach (var box in _movingSpriteBounds)
        {
            handle.DrawRect(box, Color.Transparent);
        }
    }

    private Texture? DrawBloom(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.X * BloomScale)),
            Math.Max(1, (int) MathF.Ceiling(viewport.Size.Y * BloomScale)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Target?.Size != size)
        {
            res.DisposeBloom();
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
            res.Target = _clyde.CreateRenderTarget(size, format, _smoothSampling, name: "bloom");
            res.BlurBuffer = _clyde.CreateRenderTarget(size, format, _smoothSampling, name: "bloom-blur");
        }

        var target = res.Target;
        var handle = args.WorldHandle;
        // The shader samples by pixel position, the quad only has to cover the whole target.
        var matrix = target.GetWorldToLocalMatrix(viewport.Eye!, viewport.RenderScale * (size / (Vector2) viewport.Size));
        var bounds = args.WorldBounds.Enlarged(1f);

        _bloomShaderInst.SetParameter("SCREEN_TEXTURE", ScreenTexture!);
        _bloomShaderInst.SetParameter("LIGHT_TEXTURE", viewport.LightRenderTarget.Texture);
        _bloomShaderInst.SetParameter("targetSize", (Vector2) size);
        _bloomShaderInst.SetParameter("threshold", _bloomThreshold);

        handle.RenderInRenderTarget(target,
            (Action)(() =>
            {
                handle.UseShader(_bloomShaderInst);
                handle.SetTransform(matrix);
                handle.DrawRect(bounds, Color.White);
            }), Color.Black);

        _clyde.BlurRenderTarget(viewport, target, res.BlurBuffer!, viewport.Eye!, _bloomRadius);
        return target.Texture;
    }

    protected override void DisposeBehavior()
    {
        _cvars.Dispose();
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Target;
        public IRenderTexture? BlurBuffer;

        public IRenderTexture? Emission;
        public IRenderTexture? Albedo;
        public bool HasAlbedo;

        /// <summary>
        /// Size at which making the float emission or albedo target last failed. That size is not tried again.
        /// </summary>
        public Vector2i? FailedEmissionSize;
        public Vector2i? FailedAlbedoSize;
        public IRenderTexture? Heat;
        public IRenderTexture? Wet;

        /// <summary>
        /// Station tile mask used for the last emission pass. It belongs to <see cref="GridStencilSystem"/> and is only
        /// kept here for the debug view.
        /// </summary>
        public IRenderTexture? Stencil;
        public uint EmissionFrame;
        public Box2Rotated EmissionBounds;
        public MapId EmissionMap;

        public void Dispose()
        {
            DisposeBloom();
            Emission?.Dispose();
            Emission = null;
            Albedo?.Dispose();
            Albedo = null;
            HasAlbedo = false;
            Heat?.Dispose();
            Heat = null;
            Wet?.Dispose();
            Wet = null;
        }

        public void DisposeBloom()
        {
            Target?.Dispose();
            BlurBuffer?.Dispose();
            Target = null;
            BlurBuffer = null;
        }
    }
}
