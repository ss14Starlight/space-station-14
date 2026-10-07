using System.Numerics;
using Content.Shared._Starlight.Sound;
using Content.Shared.Audio;
using Content.Shared.Inventory;
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
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public const float VacuumOcclusion = 18f;

    public const float ContactMuffle = 0.4f;

    public const float OwnMuffle = 0.15f;

    private const int OwnSearchDepth = 4;

    public const float HelmetOcclusion = 1.5f;

    public const float AmbienceHelmetOcclusion = 3.5f;

    public const float MuffleStartPressure = 60f;

    public const float ContactRange = 1.5f;

    public const float WallOcclusionMultiplier = 2.5f;

    private const float SourceMatchRange = 0.05f;

    private const float SourceClearance = 0.75f;

    private const float OwnBodyRange = 0.3f;

    private float _maxRayLength;
    private List<MapCoordinates> _openSources = new();
    private List<MapCoordinates> _openSourcesNext = new();
    private HashSet<EntityUid> _ambientParents = new();
    private HashSet<EntityUid> _ambientParentsNext = new();
    private EntityUid? _wornHelmet;
    private HashSet<EntityUid> _ownParents = new();
    private HashSet<EntityUid> _ownParentsNext = new();

    public int OcclusionCalls;

    public float ListenerMuffleValue { get; private set; }
    public float HelmetOcclusionValue { get; private set; }

    public float AmbienceOcclusionValue { get; private set; }

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
            _wornHelmet = hearing.SealedHelmet
                && _inventory.TryGetSlotEntity(_player.LocalEntity.Value, "head", out var head)
                    ? head
                    : null;
            HelmetOcclusionValue = _wornHelmet != null ? HelmetOcclusion : 0f;
        }
        else
        {
            ListenerMuffleValue = 0f;
            HelmetOcclusionValue = 0f;
            _wornHelmet = null;
        }

        AmbienceOcclusionValue = (HelmetOcclusionValue > 0f ? AmbienceHelmetOcclusion : 0f)
            + (VacuumOcclusion * ListenerMuffleValue);

        _openSourcesNext.Clear();
        _ambientParentsNext.Clear();
        _ownParentsNext.Clear();
        var local = _player.LocalEntity;
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            if (audio.Global
                || xform.MapID == MapId.Nullspace
                || (audio.Flags & AudioFlags.NoOcclusion) != 0)
                continue;

            if (HasComp<AmbientSoundComponent>(xform.ParentUid))
                _ambientParentsNext.Add(xform.ParentUid);

            if (local != null && IsOwnedBy(xform.ParentUid, local.Value))
                _ownParentsNext.Add(xform.ParentUid);

            var position = (audio.Flags & AudioFlags.GridAudio) != 0
                ? _maps.GetGridPosition(xform.ParentUid)
                : _xform.GetWorldPosition(xform);

            if (IsOpenSpace(xform.MapID, position))
                _openSourcesNext.Add(new MapCoordinates(position, xform.MapID));
        }

        (_openSources, _openSourcesNext) = (_openSourcesNext, _openSources);
        (_ambientParents, _ambientParentsNext) = (_ambientParentsNext, _ambientParents);
        (_ownParents, _ownParentsNext) = (_ownParentsNext, _ownParents);
    }

    private float GetOcclusion(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        // Racy across audio threads, good enough for a debug counter.
        OcclusionCalls++;

        // The engine's default occlusion, strengthened: how much solid stuff lies between source and listener.
        var occlusion = 0f;

        if (distance > ContactRange)
        {
            var rayLength = MathF.Min(distance - SourceClearance, _maxRayLength);
            var ray = new CollisionRay(listener.Position, delta / distance, _audio.OcclusionCollisionMask);
            occlusion = _physics.IntersectRayPenetration(listener.MapId, ray, rayLength, ignoredEnt)
                * WallOcclusionMultiplier;
        }

        var sourceInSpace = IsOpenSource(listener.MapId, listener.Position + delta);
        var muffle = MathF.Max(ListenerMuffleValue, sourceInSpace ? 1f : 0f);

        var helmet = HelmetOcclusionValue;

        if (distance < OwnBodyRange || (ignoredEnt is { } parent && _ownParents.Contains(parent)))
        {
            muffle *= OwnMuffle;

            if (ignoredEnt != null && ignoredEnt == _wornHelmet)
                helmet = 0f;
        }
        else
        {
            if (distance <= ContactRange)
                muffle *= ContactMuffle;

            if (helmet > 0f && ignoredEnt is { } ambient && _ambientParents.Contains(ambient))
                helmet = AmbienceHelmetOcclusion;
        }

        return occlusion + helmet + (VacuumOcclusion * muffle);
    }

    private bool IsOwnedBy(EntityUid uid, EntityUid owner)
    {
        for (var i = 0; i < OwnSearchDepth && uid.IsValid(); i++)
        {
            if (uid == owner)
                return true;

            uid = Transform(uid).ParentUid;
        }

        return false;
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
