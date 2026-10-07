using System.Numerics;
using Content.Shared._Starlight.Sound;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client._Starlight.Audio;

public sealed partial class VacuumHearingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public const float VacuumOcclusion = 18f;

    public const float ContactMuffle = 0.4f;

    public const float HelmetOcclusion = 1.5f;

    public const float MuffleStartPressure = 60f;

    public const float ContactRange = 1.5f;

    public const float WallOcclusionMultiplier = 2.5f;

    private const float SourceMatchRange = 0.05f;

    private float _maxRayLength;
    private List<MapCoordinates> _openSources = new();
    private List<MapCoordinates> _openSourcesNext = new();

    public int OcclusionCalls;

    public float ListenerMuffleValue { get; private set; }
    public float HelmetOcclusionValue { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesBefore.Add(typeof(AudioSystem));

        Subs.CVar(_cfg, CVars.AudioRaycastLength, value => _maxRayLength = value, true);
        _audio.GetOcclusionOverride += GetOcclusion;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _audio.GetOcclusionOverride -= GetOcclusion;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (TryComp(_player.LocalEntity, out HearingPressureComponent? hearing))
        {
            ListenerMuffleValue = Math.Clamp(1f - (hearing.Pressure / MuffleStartPressure), 0f, 1f);
            HelmetOcclusionValue = hearing.SealedHelmet ? HelmetOcclusion : 0f;
        }
        else
        {
            ListenerMuffleValue = 0f;
            HelmetOcclusionValue = 0f;
        }

        _openSourcesNext.Clear();
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            if (audio.Global
                || xform.MapID == MapId.Nullspace
                || (audio.Flags & AudioFlags.NoOcclusion) != 0)
                continue;

            var position = (audio.Flags & AudioFlags.GridAudio) != 0
                ? _maps.GetGridPosition(xform.ParentUid)
                : _xform.GetWorldPosition(xform);

            if (IsOpenSpace(xform.MapID, position))
                _openSourcesNext.Add(new MapCoordinates(position, xform.MapID));
        }

        (_openSources, _openSourcesNext) = (_openSourcesNext, _openSources);
    }

    private float GetOcclusion(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        // Racy across audio threads, good enough for a debug counter.
        OcclusionCalls++;

        // The engine's default occlusion, strengthened: how much solid stuff lies between source and listener.
        var occlusion = 0f;

        if (distance > 0.1f)
        {
            var rayLength = MathF.Min(distance, _maxRayLength);
            var ray = new CollisionRay(listener.Position, delta / distance, _audio.OcclusionCollisionMask);
            occlusion = _physics.IntersectRayPenetration(listener.MapId, ray, rayLength, ignoredEnt)
                * WallOcclusionMultiplier;
        }

        var sourceInSpace = IsOpenSource(listener.MapId, listener.Position + delta);
        var muffle = MathF.Max(ListenerMuffleValue, sourceInSpace ? 1f : 0f);

        if (distance <= ContactRange)
            muffle *= ContactMuffle;

        return occlusion + HelmetOcclusionValue + (VacuumOcclusion * muffle);
    }

    private bool IsOpenSource(MapId map, Vector2 position)
    {
        foreach (var source in _openSources)
        {
            if (source.MapId == map && (source.Position - position).LengthSquared() <= SourceMatchRange * SourceMatchRange)
                return true;
        }

        return false;
    }

    private bool IsOpenSpace(MapId map, Vector2 position)
    {
        if (!_maps.TryFindGridAt(map, position, out var grid, out var gridComp))
            return true;

        return !_maps.TryGetTileRef(grid, gridComp, _maps.WorldToTile(grid, gridComp, position), out var tile)
            || tile.Tile.IsEmpty;
    }
}
