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

    private float _maxRayLength;

    private float _listenerMuffle;
    private float _helmetOcclusion;
    private Dictionary<EntityUid, bool> _openSpace = new();
    private Dictionary<EntityUid, bool> _openSpaceNext = new();

    public int OcclusionCalls;

    public float ListenerMuffleValue => _listenerMuffle;
    public float HelmetOcclusionValue => _helmetOcclusion;

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
            _listenerMuffle = Math.Clamp(1f - hearing.Pressure / MuffleStartPressure, 0f, 1f);
            _helmetOcclusion = hearing.SealedHelmet ? HelmetOcclusion : 0f;
        }
        else
        {
            _listenerMuffle = 0f;
            _helmetOcclusion = 0f;
        }

        // The hook only knows a stream by the entity it is attached to, so look those up here.
        _openSpaceNext.Clear();
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out _, out var audio, out var xform))
        {
            if (audio.Global || _openSpaceNext.ContainsKey(xform.ParentUid))
                continue;

            var position = _xform.GetMapCoordinates(xform.ParentUid);
            _openSpaceNext[xform.ParentUid] = position.MapId != MapId.Nullspace
                && IsOpenSpace(position.MapId, position.Position);
        }

        (_openSpace, _openSpaceNext) = (_openSpaceNext, _openSpace);
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

        var sourceInSpace = ignoredEnt is { } source && _openSpace.GetValueOrDefault(source);
        var muffle = MathF.Max(_listenerMuffle, sourceInSpace ? 1f : 0f);

        if (distance <= ContactRange)
            muffle *= ContactMuffle;

        return occlusion + _helmetOcclusion + VacuumOcclusion * muffle;
    }

    private bool IsOpenSpace(MapId map, Vector2 position)
    {
        if (!_maps.TryFindGridAt(map, position, out var grid, out var gridComp))
            return true;

        return !_maps.TryGetTileRef(grid, gridComp, _maps.WorldToTile(grid, gridComp, position), out var tile)
            || tile.Tile.IsEmpty;
    }
}
