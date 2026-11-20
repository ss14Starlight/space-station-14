using System.Numerics;
using System.Runtime.InteropServices;
using Content.Client._Starlight.Overlay.Overlays;
using Content.Client.Graphics;
using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Adds one bounce of wall light to the light map before tonemapping.
/// </summary>
/// <remarks>
/// Based on Sam Bigos' 2D global illumination demo
/// (https://github.com/samuelbigos/godot_2d_global_illumination, MIT).
/// Jump flooding creates a wall distance field, then sphere tracing casts rays
/// from floor pixels to collect light from nearby walls.
/// Results build up across frames through reprojection and ray rotation, allowing
/// bounced light to spread farther over time. The final result is blurred and
/// added to the light map.
///
/// Runs only for the player's eye, before <see cref="LightTonemapOverlay"/>,
/// so bounced light is also tonemapped.
/// </remarks>
public sealed partial class GlobalIlluminationOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> _seedShader = "GiSeed";
    private static readonly ProtoId<ShaderPrototype> _jumpFloodShader = "GiJumpFlood";
    private static readonly ProtoId<ShaderPrototype> _traceShader = "GiTrace";
    private static readonly ProtoId<ShaderPrototype> _compositeShader = "GiComposite";
    private static readonly ProtoId<ShaderPrototype> _debugShader = "GiDebug";
    private static readonly ProtoId<ShaderPrototype> _copyShader = "GiCopy";
    private static readonly ProtoId<ShaderPrototype> _copyByPixelShader = "LightTonemapCopy";
    private static readonly ProtoId<ShaderPrototype> _emissionSeedShader = "GiEmissionSeed";

    /// <summary>
    /// The amount of bounce light from the previous frame retained by a pixel. Higher values look smoother but respond more slowly to changing light.
    /// </summary>
    private const float HistoryWeight = 0.85f;

    /// <summary>
    /// Upper bound on the bounce feedback, so repeated bounces always converge.
    /// </summary>
    private const float MaxFeedback = 0.8f;

    private const int FrameCycle = 64;

    /// <summary>
    /// Most occluder vertices sent in one seed draw. The engine copies each draw onto the stack, so it stays small.
    /// </summary>
    private const int MaxSeedVertices = 4096;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public const int ContentZIndex = LightTonemapOverlay.ContentZIndex - 1;

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly OccluderSystem _occluders;
    private readonly SharedTransformSystem _xform;

    private readonly ShaderInstance _seedShaderInst;
    private readonly ShaderInstance _jumpFloodShaderInst;
    private readonly ShaderInstance _traceShaderInst;
    private readonly ShaderInstance _compositeShaderInst;
    private readonly ShaderInstance _debugShaderInst;
    private readonly ShaderInstance _copyShaderInst;
    private readonly ShaderInstance _copyByPixelShaderInst;
    private readonly ShaderInstance _emissionSeedShaderInst;

    private int _frame;

    private readonly CVarSubscriptions _cvars;
    private float _resolution;
    private bool _temporal;
    private float _distance;
    private int _rays;
    private float _strength;
    private float _blur;
    private int _debug;
    private bool _emission;
    private bool _starlight;
    private bool _surfaceColor;
    private float _surfaceTint;
    private float _floorBounce;

    private readonly List<Vector2> _vertices = new(4096);
    private readonly List<ushort> _indices = new(6144);

    /// <summary>
    /// Set when float render targets can't be made.
    /// </summary>
    private bool _unsupported;

    public GlobalIlluminationOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;

        _occluders = _entMan.System<OccluderSystem>();
        _xform = _entMan.System<SharedTransformSystem>();

        _seedShaderInst = _proto.Index(_seedShader).InstanceUnique();
        _jumpFloodShaderInst = _proto.Index(_jumpFloodShader).InstanceUnique();
        _traceShaderInst = _proto.Index(_traceShader).InstanceUnique();
        _compositeShaderInst = _proto.Index(_compositeShader).InstanceUnique();
        _debugShaderInst = _proto.Index(_debugShader).InstanceUnique();
        _copyShaderInst = _proto.Index(_copyShader).InstanceUnique();
        _copyByPixelShaderInst = _proto.Index(_copyByPixelShader).InstanceUnique();
        _emissionSeedShaderInst = _proto.Index(_emissionSeedShader).InstanceUnique();

        _cvars = new CVarSubscriptions(_cfg);
        _cvars.Track(StarlightCCVars.GlobalIlluminationResolution, v => _resolution = Math.Clamp(v, 0.25f, 1f));
        _cvars.Track(StarlightCCVars.GlobalIlluminationTemporal, v => _temporal = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationDistance, v => _distance = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationRays, v => _rays = Math.Clamp(v, 4, 32));
        _cvars.Track(StarlightCCVars.GlobalIlluminationStrength, v => _strength = MathF.Max(0f, v));
        _cvars.Track(StarlightCCVars.GlobalIlluminationBlur, v => _blur = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationDebug, v => _debug = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationEmission, v => _emission = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationStarlight, v => _starlight = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationSurfaceColor, v => _surfaceColor = v);
        _cvars.Track(StarlightCCVars.GlobalIlluminationSurfaceTint, v => _surfaceTint = Math.Clamp(v, 0f, 1f));
        _cvars.Track(StarlightCCVars.GlobalIlluminationFloorBounce, v => _floorBounce = MathF.Max(0f, v));
    }

    private bool VisionLightActive()
        => _overlayManager.HasOverlay<NightVisionOverlay>() || _overlayManager.HasOverlay<ThermalVisionOverlay>();

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (_unsupported || eye == null
            || eye != _eye.CurrentEye
            || !eye.DrawLight
            || !_lightManager.Enabled
            || !_lightManager.DrawLighting
            || VisionLightActive())
            return false;

        return _entMan.TryGetComponent<MapComponent>(args.MapUid, out var map) && map.LightingEnabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var eye = viewport.Eye!;
        var light = viewport.LightRenderTarget;
        var resolution = _resolution;
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(light.Size.X * resolution)),
            Math.Max(1, (int) MathF.Ceiling(light.Size.Y * resolution)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.SeedA?.Size != size && !TryCreateTargets(res, size))
            return;

        var temporal = _temporal;
        var historyValid = temporal && res.HistoryValid && res.HistoryMap == args.MapId;

        var handle = args.WorldHandle;
        var sizeF = (Vector2) size;
        var giMatrix = res.SeedA!.GetWorldToLocalMatrix(eye, viewport.RenderScale * (sizeF / viewport.Size));
        var giBounds = args.WorldBounds.Enlarged(1f);

        // Tiles to target pixels, eye zoom and rotation included.
        var pixelsPerTile = (Vector2.Transform(Vector2.UnitX, giMatrix) - Vector2.Transform(Vector2.Zero, giMatrix)).Length();
        var maxDistance = MathF.Max(1f, _distance * pixelsPerTile);

        // 0. Glowing sprites of the last frame.
        var emission = res.Emission!;
        Texture? emissionSource = null;
        var emissionBounds = default(Box2Rotated);
        Texture? albedoSource = null;
        var hasEmission = (_emission || _starlight || _surfaceColor)
            && _overlayManager.TryGetOverlay<ScreenPostProcessOverlay>(out var post)
            && post.TryGetEmission(viewport, args.MapId, out emissionSource, out albedoSource, out emissionBounds);
        if (hasEmission)
        {
            var emissionTexture = emissionSource!;
            var sourceBounds = emissionBounds;
            handle.RenderInRenderTarget(emission,
                () =>
                {
                    handle.UseShader(_copyShaderInst);
                    handle.SetTransform(giMatrix);
                    handle.DrawTextureRect(emissionTexture, sourceBounds);
                }, Color.Black);
        }
        else
        {
            handle.RenderInRenderTarget(emission, () => { }, Color.Black);
        }

        var albedo = res.Albedo!;
        var hasAlbedo = hasEmission && albedoSource != null;
        if (hasAlbedo)
        {
            var albedoTexture = albedoSource!;
            var sourceBounds = emissionBounds;
            handle.RenderInRenderTarget(albedo,
                () =>
                {
                    handle.UseShader(_copyShaderInst);
                    handle.SetTransform(giMatrix);
                    handle.DrawTextureRect(albedoTexture, sourceBounds);
                }, Color.Transparent);
        }
        else
        {
            handle.RenderInRenderTarget(albedo, () => { }, Color.Transparent);
        }

        // 1. Seeds.
        var mapId = args.MapId;
        var queryBounds = args.WorldAABB.Enlarged(1f);
        handle.RenderInRenderTarget(res.SeedA,
            () =>
            {
                handle.UseShader(_seedShaderInst);
                handle.SetTransform(Matrix3x2.Identity);
                var state = new OccluderQueryState(this, handle, giMatrix);
                _occluders.QueryAabb(ref state,
                    static (ref OccluderQueryState state, in ComponentTreeEntry<OccluderComponent> entry) =>
                    {
                        state.Overlay.AppendPolygon(entry, ref state);
                        return true;
                    },
                    mapId,
                    queryBounds);
                Flush(handle);

                if (hasEmission)
                {
                    _emissionSeedShaderInst.SetParameter("targetSize", sizeF);
                    handle.UseShader(_emissionSeedShaderInst);
                    handle.SetTransform(giMatrix);
                    handle.DrawTextureRect(emission.Texture, giBounds);
                }
            }, Color.Transparent);

        // 2. Jump flood from the first power of two past the trace distance down to single pixels.
        var step = 1;
        while (step < maxDistance && step < Math.Max(size.X, size.Y))
            step <<= 1;

        _jumpFloodShaderInst.SetParameter("targetSize", sizeF);
        var source = res.SeedA;
        var dest = res.SeedB!;
        for (; step >= 1; step >>= 1)
        {
            _jumpFloodShaderInst.SetParameter("stepSize", (float) step);
            var input = source;
            handle.RenderInRenderTarget(dest,
                () =>
                {
                    handle.UseShader(_jumpFloodShaderInst);
                    handle.SetTransform(giMatrix);
                    handle.DrawTextureRect(input.Texture, giBounds);
                }, Color.Transparent);

            (source, dest) = (dest, source);
        }

        var sdf = source;

        // 3. Reproject the previous frame's bounce light and draw it over the world area it covered, using this frame's matrix.
        // Areas that were off screen in the previous frame remain transparent and are traced from scratch.
        var history = res.History!;
        if (historyValid)
        {
            var accumulated = res.Accumulated!;
            var previousBounds = res.HistoryBounds;
            handle.RenderInRenderTarget(history,
                () =>
                {
                    handle.UseShader(_copyShaderInst);
                    handle.SetTransform(giMatrix);
                    handle.DrawTextureRect(accumulated.Texture, previousBounds);
                }, Color.Transparent);
        }
        else
        {
            handle.RenderInRenderTarget(history, () => { }, Color.Transparent);
        }

        // 4. Trace, blended into the history.
        _traceShaderInst.SetParameter("LIGHT_TEXTURE", light.Texture);
        _traceShaderInst.SetParameter("targetSize", sizeF);
        _traceShaderInst.SetParameter("maxDistance", maxDistance);
        _traceShaderInst.SetParameter("rays", _rays);
        _traceShaderInst.SetParameter("HISTORY_TEXTURE", history.Texture);
        _traceShaderInst.SetParameter("EMISSION_TEXTURE", emission.Texture);
        _traceShaderInst.SetParameter("ALBEDO_TEXTURE", albedo.Texture);
        _traceShaderInst.SetParameter("surfaceTint", hasAlbedo ? _surfaceTint : 0f);
        _traceShaderInst.SetParameter("floorBounce", hasAlbedo ? _floorBounce : 0f);
        _traceShaderInst.SetParameter("emissionGain", hasEmission
            ? 1f / MathF.Max(_strength, 0.01f)
            : 0f);
        _traceShaderInst.SetParameter("historyWeight", temporal ? HistoryWeight : 0f);
        // Light a wall gets from bounce light is thrown back as well, a bit more of it every frame.
        _traceShaderInst.SetParameter("feedback", temporal
            ? MathF.Min(_strength, MaxFeedback)
            : 0f);
        // Rays turn a little every frame so the history averages them out.
        _frame = (_frame + 1) % FrameCycle;
        _traceShaderInst.SetParameter("frame", temporal ? (float) _frame : 0f);
        var accumulatedTarget = res.Accumulated!;
        handle.RenderInRenderTarget(accumulatedTarget,
            () =>
            {
                handle.UseShader(_traceShaderInst);
                handle.SetTransform(giMatrix);
                handle.DrawTextureRect(sdf.Texture, giBounds);
            }, Color.Black);

        res.HistoryValid = temporal;
        res.HistoryBounds = args.WorldBounds;
        res.HistoryMap = args.MapId;

        // 5. Blur a copy, the history itself has to stay sharp or it smears more every frame.
        _copyByPixelShaderInst.SetParameter("targetSize", sizeF);
        handle.RenderInRenderTarget(res.Bounce!,
            () =>
            {
                handle.UseShader(_copyByPixelShaderInst);
                handle.SetTransform(giMatrix);
                handle.DrawTextureRect(accumulatedTarget.Texture, giBounds);
            }, Color.Black);

        var blur = _blur;
        if (blur > 0f)
            _clyde.BlurRenderTarget(viewport, res.Bounce!, res.BlurBuffer!, eye, blur);

        var lightMatrix = light.GetWorldToLocalMatrix(eye, viewport.RenderScale * (light.Size / (Vector2) viewport.Size));
        var strength = _strength;
        var debug = _debug;
        ShaderInstance shader;
        if (debug is 1 or 2)
        {
            shader = _debugShaderInst;
            shader.SetParameter("SDF_TEXTURE", sdf.Texture);
            shader.SetParameter("giSize", sizeF);
            shader.SetParameter("maxDistance", maxDistance);
            shader.SetParameter("mode", debug);
        }
        else
        {
            shader = _compositeShaderInst;

            giBounds.GetCorners(out var bottomLeft, out var bottomRight, out _, out var topLeft);
            var eyePosition = eye.Position.Position;
            shader.SetParameter("FOV_TEXTURE", viewport.FovRenderTarget.Texture);
            shader.SetParameter("fovEnabled", eye.DrawFov ? 1f : 0f);
            shader.SetParameter("fovOrigin", bottomLeft - eyePosition);
            shader.SetParameter("fovAxisX", bottomRight - bottomLeft);
            shader.SetParameter("fovAxisY", topLeft - bottomLeft);
        }

        shader.SetParameter("targetSize", (Vector2) light.Size);
        shader.SetParameter("strength", strength);

        var bounce = res.Bounce!;
        handle.RenderInRenderTarget(light,
            () =>
            {
                handle.UseShader(shader);
                handle.SetTransform(lightMatrix);
                handle.DrawTextureRect(bounce.Texture, giBounds);
            }, null);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private bool TryCreateTargets(CachedResources res, Vector2i size)
    {
        res.Dispose();

        try
        {
            // Seeds represent pixel positions. Half floats cannot preserve pixel centers above 1024, so seeds use full floats.
            var seedFormat = new RenderTargetFormatParameters(RenderTargetColorFormat.RG32F);
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F);
            var smooth = new TextureSampleParameters { Filter = true };
            res.SeedA = _clyde.CreateRenderTarget(size, seedFormat, name: "gi-seed-a");
            res.SeedB = _clyde.CreateRenderTarget(size, seedFormat, name: "gi-seed-b");
            res.History = _clyde.CreateRenderTarget(size, format, name: "gi-history");
            res.Bounce = _clyde.CreateRenderTarget(size, format, smooth, name: "gi-bounce");
            res.Accumulated = _clyde.CreateRenderTarget(size, format, name: "gi-accumulated");
            res.Emission = _clyde.CreateRenderTarget(size, format, smooth, name: "gi-emission-reprojected");
            res.Albedo = _clyde.CreateRenderTarget(size, format, smooth, name: "gi-albedo-reprojected");
            res.BlurBuffer = _clyde.CreateRenderTarget(size, format, smooth, name: "gi-bounce-blur");
            return true;
        }
        catch (Exception e)
        {
            res.Dispose();
            _unsupported = true;
            _log.GetSawmill("light").Warning($"Global illumination disabled, can't create a float render target: {e.Message}");
            return false;
        }
    }

    private void AppendPolygon(in ComponentTreeEntry<OccluderComponent> entry, ref OccluderQueryState state)
    {
        var polygon = entry.Component.Polygon;
        if (polygon.Length < 3)
            return;

        // Occluders sit directly on their grid or map, the tree's own matrix is shared by all of them.
        Matrix3x2 matrix;
        if (entry.Transform.ParentUid == entry.Component.TreeUid)
        {
            if (state.TreeUid != entry.Transform.ParentUid)
            {
                state.TreeUid = entry.Transform.ParentUid;
                state.TreeToTarget = Matrix3x2.Multiply(_xform.GetWorldMatrix(entry.Transform.ParentUid), state.WorldToTarget);
            }

            matrix = Matrix3x2.Multiply(entry.Transform.LocalMatrix, state.TreeToTarget);
        }
        else
        {
            matrix = Matrix3x2.Multiply(_xform.GetWorldMatrix(entry.Transform), state.WorldToTarget);
        }

        if (_vertices.Count + polygon.Length > MaxSeedVertices)
            Flush(state.Handle);

        var indexBase = (ushort) _vertices.Count;
        foreach (var vertex in polygon)
        {
            _vertices.Add(Vector2.Transform(vertex, matrix));
        }

        for (var i = 1; i < polygon.Length - 1; i++)
        {
            _indices.Add(indexBase);
            _indices.Add((ushort) (indexBase + i));
            _indices.Add((ushort) (indexBase + i + 1));
        }
    }

    private void Flush(DrawingHandleWorld handle)
    {
        if (_vertices.Count == 0)
            return;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
            CollectionsMarshal.AsSpan(_indices),
            CollectionsMarshal.AsSpan(_vertices),
            Color.White);

        _vertices.Clear();
        _indices.Clear();
    }

    protected override void DisposeBehavior()
    {
        _cvars.Dispose();
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private struct OccluderQueryState(GlobalIlluminationOverlay overlay, DrawingHandleWorld handle, Matrix3x2 worldToTarget)
    {
        public readonly GlobalIlluminationOverlay Overlay = overlay;
        public readonly DrawingHandleWorld Handle = handle;
        public readonly Matrix3x2 WorldToTarget = worldToTarget;
        public EntityUid TreeUid = EntityUid.Invalid;
        public Matrix3x2 TreeToTarget = Matrix3x2.Identity;
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? SeedA;
        public IRenderTexture? SeedB;
        public IRenderTexture? Bounce;
        public IRenderTexture? BlurBuffer;

        /// <summary>
        /// Unblurred bounce light of the last frame, the history the next one blends into.
        /// </summary>
        public IRenderTexture? Accumulated;

        /// <summary>
        /// <see cref="Accumulated"/> moved into this frame's view, what the trace blends into.
        /// </summary>
        public IRenderTexture? History;
        /// <summary>
        /// Glowing sprites of the last frame in this frame's pixels.
        /// </summary>
        public IRenderTexture? Emission;

        /// <summary>
        /// Surface colours of the last frame in this frame's pixels.
        /// </summary>
        public IRenderTexture? Albedo;
        public bool HistoryValid;
        public Box2Rotated HistoryBounds;
        public MapId HistoryMap;

        public void Dispose()
        {
            Accumulated?.Dispose();
            Accumulated = null;
            Emission?.Dispose();
            Emission = null;
            Albedo?.Dispose();
            Albedo = null;
            HistoryValid = false;
            SeedA?.Dispose();
            SeedB?.Dispose();
            Bounce?.Dispose();
            BlurBuffer?.Dispose();
            History?.Dispose();
            SeedA = SeedB = Bounce = BlurBuffer = History = null;
        }
    }
}
