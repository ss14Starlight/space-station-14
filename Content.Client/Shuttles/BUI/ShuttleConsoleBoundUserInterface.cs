using Content.Client._Starlight.UserInterface; // Starlight
using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

#region Starlight
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Player;
#endregion Starlight

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed class ShuttleConsoleBoundUserInterface : BoundUserInterface
{
    #region Starlight
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    #endregion Starlight

    [ViewVariables]
    private ShuttleConsoleWindow? _window;

    public ShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this); // Starlight
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreatePopOutableWindow<ShuttleConsoleWindow>(EntMan); // Starlight: popout support

        _window.RequestFTL += OnFTLRequest;
        _window.RequestBeaconFTL += OnFTLBeaconRequest;
        _window.DockRequest += OnDockRequest;
        _window.UndockRequest += OnUndockRequest;
        _window.RadarClicked += OnRadarClicked; // Starlight
    }

    private void OnUndockRequest(NetEntity entity)
    {
        SendMessage(new UndockRequestMessage()
        {
            DockEntity = entity,
        });
    }

    private void OnDockRequest(NetEntity entity, NetEntity target)
    {
        SendMessage(new DockRequestMessage()
        {
            DockEntity = entity,
            TargetDockEntity = target,
        });
    }

    private void OnFTLBeaconRequest(NetEntity ent, Angle angle)
    {
        SendMessage(new ShuttleConsoleFTLBeaconMessage()
        {
            Beacon = ent,
            Angle = angle,
        });
    }

    private void OnFTLRequest(MapCoordinates obj, Angle angle)
    {
        SendMessage(new ShuttleConsoleFTLPositionMessage()
        {
            Coordinates = obj,
            Angle = angle,
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Starlight - start
            if (_window != null)
                _window.RadarClicked -= OnRadarClicked;
            // Starlight - end
            _window?.DisposePopOut(); // Starlight: close the popout if exists
        }
    }

    #region Starlight
    private void OnRadarClicked(EntityCoordinates coordinates)
    {
        var local = _playerManager.LocalEntity;
        if (local is null || !EntMan.HasComponent<StationAiHeldComponent>(local.Value))
            return;

        SendMessage(new CrewMonitoringWarpRequestMessage(EntMan.GetNetCoordinates(coordinates)));
    }
    #endregion Starlight

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ShuttleBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(Owner, cState);
    }
}
