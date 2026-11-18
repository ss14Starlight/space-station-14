using System.Numerics;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Light.ColorGrading;
using Content.Shared._Starlight.Zones;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Light;

public sealed partial class ColorGradingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// Seconds for the grade to get about two thirds of the way to a new one.
    /// </summary>
    private const float FadeTime = 0.6f;

    /// <summary>
    /// The grade to apply this frame, already scaled by the player's strength setting.
    /// </summary>
    public Grade Current { get; private set; } = Grade.Identity;

    private Grade _smoothed = Grade.Identity;
    private string _preview = string.Empty;
    private bool _enabled;
    private float _strength;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.ColorGradePreview, v => _preview = v, true);
        Subs.CVar(_cfg, StarlightCCVars.ColorGrading, v => _enabled = v, true);
        Subs.CVar(_cfg, StarlightCCVars.ColorGradingStrength, v => _strength = Math.Clamp(v, 0f, 1f), true);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var target = Grade.Identity;
        if (_preview != string.Empty)
        {
            if (_proto.TryIndex<ColorGradePrototype>(_preview, out var previewProto))
                target = Grade.From(previewProto);
        }
        else if (_enabled && _player.LocalEntity is { } player)
        {
            if (TryComp<ZoneTrackerComponent>(player, out var tracker)
                && tracker.Zone is { } zone
                && _proto.TryIndex(zone, out var zoneProto)
                && zoneProto.ColorGrade is { } zoneGrade
                && _proto.TryIndex(zoneGrade, out var zoneGradeProto))
            {
                target = target.Then(Grade.From(zoneGradeProto));
            }

            if (Transform(player).GridUid is { } grid
                && TryComp<StationColorGradeComponent>(grid, out var station)
                && station.AlertGrade is { } alertGrade
                && _proto.TryIndex(alertGrade, out var alertGradeProto))
            {
                target = target.Then(Grade.From(alertGradeProto));
            }
        }

        var t = 1f - MathF.Exp(-frameTime / FadeTime);
        _smoothed = Grade.Lerp(_smoothed, target, t);

        Current = Grade.Lerp(Grade.Identity, _smoothed, _strength);
    }

    /// <summary>
    /// Color grade parameters as the shader takes them.
    /// </summary>
    public readonly record struct Grade(Vector3 Tint, Vector3 Shadows, float Saturation, float Contrast)
    {
        public static readonly Grade Identity = new(Vector3.One, Vector3.Zero, 1f, 1f);

        public bool IsIdentity
            => Vector3.DistanceSquared(Tint, Vector3.One) < 1e-6f
            && Shadows.LengthSquared() < 1e-6f
            && MathF.Abs(Saturation - 1f) < 1e-3f
            && MathF.Abs(Contrast - 1f) < 1e-3f;

        public static Grade From(ColorGradePrototype proto)
        {
            var tint = Color.FromSrgb(proto.Tint);
            var shadows = Color.FromSrgb(proto.Shadows);
            return new Grade(new Vector3(tint.R, tint.G, tint.B),
                new Vector3(shadows.R, shadows.G, shadows.B),
                proto.Saturation,
                proto.Contrast);
        }

        /// <summary>
        /// This grade followed by <paramref name="other"/>, roughly, tints and the saturation and contrast factors multiply,
        /// shadow colours add up.
        /// </summary>
        public Grade Then(Grade other)
            => new(Tint * other.Tint, Shadows + other.Shadows, Saturation * other.Saturation, Contrast * other.Contrast);

        public static Grade Lerp(Grade a, Grade b, float t)
            => new(Vector3.Lerp(a.Tint, b.Tint, t),
                Vector3.Lerp(a.Shadows, b.Shadows, t),
                a.Saturation + ((b.Saturation - a.Saturation) * t),
                a.Contrast + ((b.Contrast - a.Contrast) * t));
    }
}
