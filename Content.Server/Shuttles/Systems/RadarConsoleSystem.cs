using System.Numerics;
using Content.Server.Shuttles.Components; // _Starlight
using Content.Server.UserInterface;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.PowerCell;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing; // _Starlight

namespace Content.Server.Shuttles.Systems;

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!; // _Starlight
    [Dependency] private readonly RadarLaserSystem _laserSystem = default!; // _Starlight
    [Dependency] private readonly IGameTiming _timing = default!; // _Starlight

    // _Starlight - periodic blip/laser update
    // How often (in seconds) to push fresh blip state to all open radar consoles.
    private const float BlipUpdateInterval = 0.25f;
    private float _blipUpdateTimer = 0f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);
    }

    public override void Update(float frameTime) // _Starlight
    {
        base.Update(frameTime);
        _blipUpdateTimer += frameTime;
        if (_blipUpdateTimer < BlipUpdateInterval)
            return;
        _blipUpdateTimer = 0f;

        // _Starlight - prune expired Apollo laser traces before syncing state
        _laserSystem.PruneExpiredTraces((float)_timing.CurTime.TotalSeconds);

        var query = AllEntityQuery<RadarConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateState(uid, comp);
        }
    }

    private void OnRadarStartup(EntityUid uid, RadarConsoleComponent component, ComponentStartup args)
    {
        UpdateState(uid, component);
    }

    protected override void UpdateState(EntityUid uid, RadarConsoleComponent component)
    {
        var xform = Transform(uid);
        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (component.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (_uiSystem.HasUi(uid, RadarConsoleUiKey.Key))
        {
            NavInterfaceState state;
            var docks = _console.GetAllDocks();

            if (coordinates != null && angle != null)
            {
                state = _console.GetNavState(uid, docks, coordinates.Value, angle.Value);
            }
            else
            {
                state = _console.GetNavState(uid, docks);
            }

            state.RotateWithEntity = !component.FollowEntity;

            // _Starlight - populate blips and laser traces
            // Populate radar blips for entities with RadarBlipComponent (e.g. artillery shells)
            var consoleMapCoords = _transformSystem.GetMapCoordinates(uid);
            var maxRangeSq = state.MaxRange * state.MaxRange;
            var blipQuery = AllEntityQuery<RadarBlipComponent, TransformComponent>();
            while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform))
            {
                if (blip.RequireInSpace && blipXform.GridUid != null)
                    continue;
                if (blipXform.MapID != consoleMapCoords.MapId)
                    continue;
                var blipMapCoords = _transformSystem.GetMapCoordinates(blipUid, blipXform);
                if ((blipMapCoords.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                    continue;
                state.Blips.Add(new RadarBlipData(GetNetCoordinates(blipXform.Coordinates), blip.Color, blip.Scale, blip.Shape)); // _Starlight - shape
            }

            // _Starlight - Apollo hitscan laser beam traces
            // Populate laser traces from hitscan guns with RadarLaserTrackerComponent.
            var laserQuery = AllEntityQuery<RadarLaserTrackerComponent, TransformComponent>();
            while (laserQuery.MoveNext(out var laserUid, out var tracker, out var laserXform))
            {
                if (laserXform.MapID != consoleMapCoords.MapId)
                    continue;
                foreach (var (origin, dir, _) in tracker.Traces)
                {
                    // Only show traces from guns within radar range.
                    if ((origin.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                        continue;
                    state.Lasers.Add(new RadarLaserData(
                        GetNetCoordinates(laserXform.Coordinates),
                        dir,
                        tracker.MaxRange,
                        tracker.LaserColor));
                }
            }

            _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, new NavBoundUserInterfaceState(state));
        }
    }
}
