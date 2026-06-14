using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Starfall.Particles;

/// <summary>Draws all live particles for every active emitter each frame.</summary>
public sealed class ParticleOverlay : Overlay
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly ParticleSystem _system;

    // Shader cache
    private readonly Dictionary<string, ShaderInstance?> _shaderCache = new();

    private readonly List<ActiveEmitter> _sortBuffer = new();
    private static readonly Comparison<ActiveEmitter> RenderLayerComparison =
        (a, b) => (a.Overrides?.RenderLayer ?? a.Proto.RenderLayer)
            .CompareTo(b.Overrides?.RenderLayer ?? b.Proto.RenderLayer);

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public ParticleOverlay(ParticleSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var mapId = args.MapId;
        var eyeAngle = (float)_eye.CurrentEye.Rotation;
        var cosR = MathF.Cos(-eyeAngle);
        var sinR = MathF.Sin(-eyeAngle);

        // Sort emitters, lowest layers render first
        _sortBuffer.Clear();
        foreach (var emitter in _system.GetEmitters())
        {
            if (emitter.MapCoords.MapId != mapId)
                continue;
            if (!args.WorldBounds.Contains(emitter.MapCoords.Position))
                continue;
            if (emitter.Frames.Length == 0)
                continue;

            _sortBuffer.Add(emitter);
        }

        if (_sortBuffer.Count == 0)
            return;

        _sortBuffer.Sort(RenderLayerComparison);

        string? activeShader = null; // track to avoid redundant calls

        foreach (var emitter in _sortBuffer)
        {

            var proto = emitter.Proto;
            var ovr = emitter.Overrides;
            var tex = emitter.Frames[emitter.AnimFrame];
            var baseHalfSize = (ovr?.ParticleSize ?? proto.ParticleSize) * 0.5f;

            // Resolve shader override takes precedence, then prototype, then null
            string? wantedShader = ovr?.Shader ?? (string.IsNullOrEmpty(proto.Shader) ? null : proto.Shader);

            if (wantedShader != activeShader)
            {
                if (wantedShader != null)
                {
                    if (!_shaderCache.TryGetValue(wantedShader, out var cached))
                    {
                        cached = _proto.TryIndex<ShaderPrototype>(wantedShader, out var shaderProto)
                            ? shaderProto.Instance()
                            : null;
                        _shaderCache[wantedShader] = cached;
                    }
                    handle.UseShader(cached);
                }
                else
                {
                    handle.UseShader(null);
                }
                activeShader = wantedShader;
            }

            var screenOrigin = emitter.MapCoords.Position;

            foreach (var particle in emitter.Particles)
            {
                if (!particle.Alive) continue;

                var t = particle.AgeRatio;

                // Color: use ColorOverLifetime gradient if available, otherwise lerp StartColor to EndColor
                Color color;
                if (proto.ColorOverLifetime.Count > 0)
                    color = ParticleSystem.SampleColorCurve(proto.ColorOverLifetime, t);
                else
                {
                    var startColor = ovr?.StartColor ?? proto.StartColor;
                    var endColor   = ovr?.EndColor   ?? proto.EndColor;
                    color = Color.InterpolateBetween(startColor, endColor, t);
                }

                // ColorOverride tint
                var tintColor = ovr?.ColorOverride ?? emitter.ColorOverride;
                if (tintColor is { } tint)
                    color = new Color(color.R * tint.R, color.G * tint.G, color.B * tint.B, color.A * tint.A);

                // AlphaOverLifetime: multiplied on top of color alpha
                if (proto.AlphaOverLifetime.Count > 0)
                {
                    var alpha = ParticleSystem.SampleCurve(proto.AlphaOverLifetime, t);
                    color = color.WithAlpha(color.A * alpha);
                }

                // Size: base * intensity * SizeMultiplier * SizeOverLifetime curve
                var halfSize = baseHalfSize * particle.SpawnIntensity * particle.SizeMultiplier;
                if (proto.SizeOverLifetime.Count > 0)
                    halfSize *= ParticleSystem.SampleCurve(proto.SizeOverLifetime, t);

                // Convert screen-space LocalOffset to world offset
                var local = particle.LocalOffset;
                var worldOffset = new Vector2(local.X * cosR - local.Y * sinR,
                                              local.X * sinR + local.Y * cosR);

                var origin = proto.WorldSpace ? particle.SpawnOrigin : screenOrigin;
                var worldPos = origin + worldOffset;

                // StretchFactor: elongate along velocity direction proportional to speed.
                // Rotation is derived from the velocity unit vector +precomputed eye cos/sin
                var stretchFactor = ovr?.StretchFactor ?? proto.StretchFactor;
                if (stretchFactor > 0f)
                {
                    var velLenSq = particle.Velocity.LengthSquared();
                    if (velLenSq > 0.001f * 0.001f)
                    {
                        var velLen = MathF.Sqrt(velLenSq);
                        var stretchY = 1f + velLen * stretchFactor;
                        // Rotate velocity unit vector by -eyeAngle using precomputed cosR/sinR.
                        // ux = vel.X/velLen,  uy = vel.Y/velLen
                        // cV = cos(-eye+velAngle) = cosR*uy - sinR*ux
                        // sV = sin(-eye+velAngle) = sinR*uy + cosR*ux
                        var invLen = 1f / velLen;
                        var ux = particle.Velocity.X * invLen;
                        var uy = particle.Velocity.Y * invLen;
                        var cV = cosR * uy - sinR * ux;
                        var sV = sinR * uy + cosR * ux;
                        handle.SetTransform(new Matrix3x2(cV, sV, -sV, cV, worldPos.X, worldPos.Y));
                        handle.DrawTextureRect(tex,
                            new Box2(-halfSize, -halfSize * stretchY, halfSize, halfSize * stretchY),
                            color);
                        continue;
                    }
                }

                // AlignToVelocity: rotate sprite to face its velocity direction.
                if (proto.AlignToVelocity)
                {
                    var velLenSq = particle.Velocity.LengthSquared();
                    if (velLenSq > 0.001f * 0.001f)
                    {
                        var invLen = 1f / MathF.Sqrt(velLenSq);
                        var ux = particle.Velocity.X * invLen;
                        var uy = particle.Velocity.Y * invLen;
                        var cos = cosR * uy - sinR * ux;
                        var sin = sinR * uy + cosR * ux;
                        handle.SetTransform(new Matrix3x2(cos, sin, -sin, cos, worldPos.X, worldPos.Y));
                        handle.DrawTextureRect(tex, new Box2(-halfSize, -halfSize, halfSize, halfSize), color);
                        continue;
                    }
                }

                // Draw with rotation applied. Rotation is in radians, positive is clockwise, and 0 means "facing up" (aligned with SCREEN/eye/whatever Y axis).
                var totalRotation = -eyeAngle + particle.Rotation;
                var cosP = MathF.Cos(totalRotation);
                var sinP = MathF.Sin(totalRotation);
                handle.SetTransform(new Matrix3x2(cosP, sinP, -sinP, cosP, worldPos.X, worldPos.Y));
                handle.DrawTextureRect(tex, new Box2(-halfSize, -halfSize, halfSize, halfSize), color);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }
}
