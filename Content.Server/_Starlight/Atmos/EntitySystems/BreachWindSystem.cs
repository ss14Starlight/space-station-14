using Content.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Atmos.EntitySystems;

public sealed partial class BreachWindSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;
    [Dependency] private SharedMapSystem _maps = default!;

    private static readonly EntProtoId _windPrototype = "BreachWindSound";

    private static readonly TimeSpan _linger = TimeSpan.FromSeconds(2);

    private const int PatchSize = 4;

    private const float QuietPressure = 15f;
    private const float LoudPressure = 250f;
    private const float MinVolume = -12f;
    private const float MaxVolume = 2f;

    private readonly Dictionary<(EntityUid Grid, Vector2i Patch), Wind> _winds = new();
    private readonly List<(EntityUid Grid, Vector2i Patch)> _expired = new();

    private sealed class Wind
    {
        public EntityUid Entity;
        public TimeSpan Until;
        public float Volume;
    }

    public void ReportFlow(EntityUid grid, Vector2i tile, float pressureDifference)
    {
        var patch = new Vector2i(
            (int) MathF.Floor(tile.X / (float) PatchSize),
            (int) MathF.Floor(tile.Y / (float) PatchSize));
        var key = (grid, patch);
        var volume = VolumeFor(pressureDifference);

        if (_winds.TryGetValue(key, out var wind) && Exists(wind.Entity))
        {
            wind.Until = _timing.CurTime + _linger;

            // Follow the strongest flow in the patch, ignore small wobbles to keep network traffic down.
            if (volume > wind.Volume + 1f || volume < wind.Volume - 4f)
            {
                wind.Volume = volume;
                _ambient.SetVolume(wind.Entity, volume);
            }

            return;
        }

        var entity = Spawn(_windPrototype, _maps.ToCenterCoordinates(grid, tile));
        _ambient.SetVolume(entity, volume);
        _winds[key] = new Wind { Entity = entity, Until = _timing.CurTime + _linger, Volume = volume };
    }

    private static float VolumeFor(float pressureDifference)
    {
        var t = Math.Clamp((pressureDifference - QuietPressure) / (LoudPressure - QuietPressure), 0f, 1f);
        return MinVolume + (MaxVolume - MinVolume) * t;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_winds.Count == 0)
            return;

        var now = _timing.CurTime;
        _expired.Clear();

        foreach (var (key, wind) in _winds)
        {
            if (now >= wind.Until || !Exists(wind.Entity))
                _expired.Add(key);
        }

        foreach (var key in _expired)
        {
            if (_winds.Remove(key, out var wind) && Exists(wind.Entity))
                QueueDel(wind.Entity);
        }
    }
}
