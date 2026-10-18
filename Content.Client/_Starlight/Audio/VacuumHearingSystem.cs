using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Sound;
using Content.Shared.Audio;
using Content.Shared.Doors.Components;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Audio;

public sealed partial class VacuumHearingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private EntityQuery<OccluderComponent> _occluderQuery = default!;
    [Dependency] private EntityQuery<DoorComponent> _doorQuery = default!;

    private static readonly ProtoId<TagPrototype> _windowTag = "Window";

    public const float SilentOcclusion = 50f;
    public const float StructureOcclusion = 6f;
    public const float StructureOcclusionPerTile = 2.5f;
    private const float AmbienceStructureOcclusion = 6f;
    public const float OwnOcclusion = 2.7f;
    public const float HelmetOcclusion = 1.5f;
    public const float AmbienceHelmetOcclusion = 3.5f;
    public const float MuffleStartPressure = 60f;
    private const float AirAbsorptionPerTile = 0.07f;
    private const float VacuumWallFactor = 0.5f;
    private const float WallWeight = 2.5f;
    private const float DoorWeight = 1.8f;
    private const float WindowWeight = 1.5f;
    private const float FlimsyWeight = 0.25f;

    private const int OcclusionMask = (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable);

    private const float ContactRange = 1.5f;

    private const float SourceMatchRange = 0.05f;

    private const float SourceClearance = 0.75f;

    private const float OwnBodyRange = 0.3f;

    private const int OwnSearchDepth = 4;

    private readonly record struct SourceInfo(MapCoordinates Position, bool OpenSpace, EntityUid? Grid);

    private float _maxRayLength;
    private List<SourceInfo> _sources = new();
    private List<SourceInfo> _sourcesNext = new();
    private HashSet<EntityUid> _ambientParents = new();
    private HashSet<EntityUid> _ambientParentsNext = new();
    private HashSet<EntityUid> _ownParents = new();
    private HashSet<EntityUid> _ownParentsNext = new();
    private EntityUid? _wornHelmet;
    private EntityUid? _listenerGrid;
    private bool _listenerGrounded;

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

        var local = _player.LocalEntity;

        if (TryComp(local, out HearingPressureComponent? hearing))
        {
            ListenerMuffleValue = Math.Clamp(1f - (hearing.Pressure / MuffleStartPressure), 0f, 1f);
            _wornHelmet = hearing.SealedHelmet
                && _inventory.TryGetSlotEntity(local.Value, "head", out var head)
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

        _listenerGrid = local != null ? Transform(local.Value).GridUid : null;
        _listenerGrounded = local != null && _listenerGrid != null && !_gravity.IsWeightless(local.Value);

        var ambienceVacuum = _listenerGrounded ? StructureOcclusion + AmbienceStructureOcclusion : SilentOcclusion;
        AmbienceOcclusionValue = (HelmetOcclusionValue > 0f ? AmbienceHelmetOcclusion : 0f)
            + (ambienceVacuum * ListenerMuffleValue);

        _sourcesNext.Clear();
        _ambientParentsNext.Clear();
        _ownParentsNext.Clear();
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out _, out var audio, out var xform))
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

            var open = IsOpenSpace(xform.MapID, position, out var grid);
            _sourcesNext.Add(new SourceInfo(new MapCoordinates(position, xform.MapID), open, grid));
        }

        (_sources, _sourcesNext) = (_sourcesNext, _sources);
        (_ambientParents, _ambientParentsNext) = (_ambientParentsNext, _ambientParents);
        (_ownParents, _ownParentsNext) = (_ownParentsNext, _ownParents);
    }

    private float GetOcclusion(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        // Racy across audio threads, good enough for a debug counter.
        OcclusionCalls++;

        var source = FindSource(listener.MapId, listener.Position + delta);
        var own = distance < OwnBodyRange || (ignoredEnt is { } parent && _ownParents.Contains(parent));

        var vacuum = MathF.Max(ListenerMuffleValue, source is { OpenSpace: true } ? 1f : 0f);

        var walls = 0f;
        if (!own && distance > ContactRange)
        {
            walls = GetWallOcclusion(listener, delta, distance, ignoredEnt)
                * (1f - (vacuum * (1f - VacuumWallFactor)));
        }

        float vacuumOcclusion;
        if (own)
            vacuumOcclusion = OwnOcclusion;
        else if (CarriesThroughStructure(source))
            vacuumOcclusion = StructureOcclusion + (StructureOcclusionPerTile * distance);
        else
            vacuumOcclusion = SilentOcclusion;

        var helmet = HelmetOcclusionValue;
        if (own)
        {
            if (ignoredEnt != null && ignoredEnt == _wornHelmet)
                helmet = 0f;
        }
        else if (helmet > 0f && ignoredEnt is { } ambient && _ambientParents.Contains(ambient))
        {
            helmet = AmbienceHelmetOcclusion;
        }

        var air = distance * AirAbsorptionPerTile * (1f - vacuum);

        return walls + helmet + air + (vacuumOcclusion * vacuum);
    }

    private bool CarriesThroughStructure(SourceInfo? source)
    {
        return _listenerGrounded
            && source is { OpenSpace: false, Grid: { } grid }
            && grid == _listenerGrid;
    }

    private float GetWallOcclusion(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        var rayLength = MathF.Min(distance - SourceClearance, _maxRayLength);
        if (rayLength <= 0f)
            return 0f;

        var ray = new CollisionRay(listener.Position, delta / distance, OcclusionMask);

        var hits = _physics.IntersectRayWithPredicate(listener.MapId,
                ray,
                ignoredEnt,
                static (uid, ignored) => uid == ignored,
                rayLength,
                returnOnFirstHit: false)
            .ToList();

        var occlusion = 0f;
        for (var i = 0; i < hits.Count; i++)
        {
            var uid = hits[i].HitEntity;
            var seen = false;
            for (var j = 0; j < i && !seen; j++)
            {
                seen = hits[j].HitEntity == uid;
            }

            if (!seen)
                occlusion += ObstacleWeight(uid);
        }

        return occlusion;
    }

    private float ObstacleWeight(EntityUid uid)
    {
        if (_occluderQuery.TryComp(uid, out var occluder) && occluder.Enabled)
            return WallWeight;

        if (_doorQuery.HasComp(uid))
            return DoorWeight;

        if (_tag.HasTag(uid, _windowTag))
            return WindowWeight;

        return FlimsyWeight;
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

    private SourceInfo? FindSource(MapId map, Vector2 position)
    {
        foreach (var source in _sources)
        {
            if (source.Position.MapId == map
                && (source.Position.Position - position).LengthSquared() <= SourceMatchRange * SourceMatchRange)
                return source;
        }

        return null;
    }

    private bool IsOpenSpace(MapId map, Vector2 position, out EntityUid? grid)
    {
        grid = null;

        if (!_maps.TryFindGridAt(map, position, out var gridUid, out var gridComp))
            return true;

        grid = gridUid;
        return !_maps.TryGetTileRef(gridUid, gridComp, _maps.WorldToTile(gridUid, gridComp, position), out var tile)
            || tile.Tile.IsEmpty;
    }
}
