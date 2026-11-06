using Content.Shared._Starlight.Shuttles.BUIStates;
using Content.Shared._Starlight.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Replay;

// The server builds radar state for live observers; replays have to build it locally. Docking ports and laser traces
// are server-side only, so they're left out.
public sealed partial class ReplayObserverSystem
{
    private const float RadarUpdateInterval = 0.25f;

    private float _radarAccumulator;

    private void UpdateRadar(float frameTime)
    {
        _radarAccumulator += frameTime;
        if (_radarAccumulator < RadarUpdateInterval)
            return;

        _radarAccumulator = 0f;

        if (_observer is not { } observer
            || !TryComp<RadarConsoleComponent>(observer, out var radar)
            || !_ui.IsUiOpen(observer, RadarConsoleUiKey.Key))
        {
            return;
        }

        var state = new NavInterfaceState(radar.MaxRange, GetNetCoordinates(new EntityCoordinates(observer, default)), Angle.Zero)
        {
            RotateWithEntity = false,
        };

        var origin = _transform.GetMapCoordinates(observer);
        var maxRangeSq = radar.MaxRange * radar.MaxRange;
        var blips = AllEntityQuery<RadarBlipComponent, TransformComponent>();
        while (blips.MoveNext(out var uid, out var blip, out var xform))
        {
            if (blip.RequireInSpace && xform.GridUid != null)
                continue;

            var pos = _transform.GetMapCoordinates(uid, xform);
            if (pos.MapId != origin.MapId || (pos.Position - origin.Position).LengthSquared() > maxRangeSq)
                continue;

            state.Blips.Add(new RadarBlipData(GetNetCoordinates(xform.Coordinates), blip.Color, blip.Scale, blip.Shape));
        }

        _ui.SetUiState(observer, RadarConsoleUiKey.Key, new NavBoundUserInterfaceState(state, new DockingPortStates(new())));
    }
}
