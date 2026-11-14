using System.Numerics;
using System.Runtime.InteropServices;
using Content.Client.Graphics;
using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

/// <summary>
/// One bounce of light off walls, added onto the engine's light map before the tonemap.
/// </summary>
/// <remarks>
/// <para>
/// Based on the jump flood + sphere tracing approach of Sam Bigos' 2D global illumination demo
/// (https://github.com/samuelbigos/godot_2d_global_illumination, MIT), adapted to keep the engine's direct light:
/// </para>
/// <list type="number">
/// <item>Occluder polygons are drawn into a seed target, every covered pixel storing its own position.</item>
/// <item>Jump flood passes spread the seeds, so every pixel knows its nearest wall pixel (a distance field).
/// Steps only start at the trace distance, walls further away than that never matter.</item>
/// <item>Every floor pixel casts rays through the distance field. A ray that hits a wall takes the light that wall
/// pixel has in the light map: the engine's wall bleed lights the faces next to lit floor.</item>
/// <item>The trace is blended into last frame's result, reprojected to this frame's view, and rays turn a little every
/// frame, so noise averages out over time. Rays that hit a wall also take the bounce light already lying next to it
/// in that history, which gives more bounces over a few frames, the way Bigos' demo does it.</item>
/// <item>A copy is blurred to hide what noise is left and added onto the light map.</item>
/// </list>
/// <para>
/// Runs only for the player's own eye, before <see cref="LightTonemapOverlay"/> so bounced light is rolled off too.
/// </para>
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

    /// <summary>
    /// How much of last frame's bounce light a pixel keeps. Higher is smoother, but lags behind changing light.
    /// </summary>
    private const float HistoryWeight = 0.85f;

    /// <summary>
    /// Upper bound on the bounce feedback, so repeated bounces always converge.
    /// </summary>
    private const float MaxFeedback = 0.8f;

    private const int FrameCycle = 64;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPrototypeManager _proto = default!;

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

    private int _frame;

    private readonly List<Vector2> _vertices = new(4096);
    private readonly List<ushort> _indices = new(6144);

    /// <summary>
    /// Set when float render targets can't be made: seeds need more than 8 bits per channel.
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
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (_unsupported || eye == null || eye != _eye.CurrentEye || !eye.DrawLight
            || !_lightManager.Enabled || !_lightManager.DrawLighting)
        {
            return false;
        }

        return _entMan.TryGetComponent<MapComponent>(args.MapUid, out var map) && map.LightingEnabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var eye = viewport.Eye!;
        var light = viewport.LightRenderTarget;
        var resolution = Math.Clamp(_cfg.GetCVar(StarlightCCVars.GlobalIlluminationResolution), 0.25f, 1f);
        var size = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(light.Size.X * resolution)),
            Math.Max(1, (int) MathF.Ceiling(light.Size.Y * resolution)));

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.SeedA?.Size != size && !TryCreateTargets(res, size))
            return;

        var temporal = _cfg.GetCVar(StarlightCCVars.GlobalIlluminationTemporal);
        var historyValid = temporal && res.HistoryValid && res.HistoryMap == args.MapId;

        var handle = args.WorldHandle;
        var sizeF = (Vector2) size;
        var giMatrix = res.SeedA!.GetWorldToLocalMatrix(eye, viewport.RenderScale * (sizeF / viewport.Size));
        var giBounds = args.WorldBounds.Enlarged(1f);

        // Tiles to target pixels, eye zoom and rotation included.
        var pixelsPerTile = (Vector2.Transform(Vector2.UnitX, giMatrix) - Vector2.Transform(Vector2.Zero, giMatrix)).Length();
        var maxDistance = MathF.Max(1f, _cfg.GetCVar(StarlightCCVars.GlobalIlluminationDistance) * pixelsPerTile);

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
            }, Color.Transparent);

        // 2. Jump flood, from the first power of two past the trace distance down to single pixels.
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

        // 3. Last frame's bounce light, reprojected: drawn over the world area it covered with this frame's matrix.
        // Everything that was off screen last frame stays transparent and is traced from scratch.
        var history = dest;
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
        _traceShaderInst.SetParameter("rays", Math.Clamp(_cfg.GetCVar(StarlightCCVars.GlobalIlluminationRays), 4, 32));
        _traceShaderInst.SetParameter("HISTORY_TEXTURE", history.Texture);
        _traceShaderInst.SetParameter("historyWeight", temporal ? HistoryWeight : 0f);
        // Light a wall gets from bounce light is thrown back as well, a bit more of it every frame.
        _traceShaderInst.SetParameter("feedback", temporal
            ? Math.Clamp(_cfg.GetCVar(StarlightCCVars.GlobalIlluminationStrength), 0f, MaxFeedback)
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

        var blur = _cfg.GetCVar(StarlightCCVars.GlobalIlluminationBlur);
        if (blur > 0f)
            _clyde.BlurRenderTarget(viewport, res.Bounce!, res.BlurBuffer!, eye, blur);

        var lightMatrix = light.GetWorldToLocalMatrix(eye, viewport.RenderScale * (light.Size / (Vector2) viewport.Size));
        var strength = MathF.Max(0f, _cfg.GetCVar(StarlightCCVars.GlobalIlluminationStrength));
        var debug = _cfg.GetCVar(StarlightCCVars.GlobalIlluminationDebug);
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
            var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F);
            res.SeedA = _clyde.CreateRenderTarget(size, format, name: "gi-seed-a");
            res.SeedB = _clyde.CreateRenderTarget(size, format, name: "gi-seed-b");
            res.Bounce = _clyde.CreateRenderTarget(size, format, name: "gi-bounce");
            res.Accumulated = _clyde.CreateRenderTarget(size, format, name: "gi-accumulated");
            res.BlurBuffer = _clyde.CreateRenderTarget(size, format, name: "gi-bounce-blur");
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

        if (_vertices.Count + polygon.Length > ushort.MaxValue)
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
        public bool HistoryValid;
        public Box2Rotated HistoryBounds;
        public MapId HistoryMap;

        public void Dispose()
        {
            Accumulated?.Dispose();
            Accumulated = null;
            HistoryValid = false;
            SeedA?.Dispose();
            SeedB?.Dispose();
            Bounce?.Dispose();
            BlurBuffer?.Dispose();
            SeedA = SeedB = Bounce = BlurBuffer = null;
        }
    }
}
