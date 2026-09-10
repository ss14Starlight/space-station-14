using Content.Client._Starlight.UserInterface;
using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.BUIStates;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using RadarConsoleWindow = Content.Client.Shuttles.UI.RadarConsoleWindow;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Shuttles.BUI;

[UsedImplicitly]
public sealed class RadarConsoleBoundUserInterface : BoundUserInterface
{
    #region Starlight
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    #endregion
    [ViewVariables]
    private RadarConsoleWindow? _window;

    public RadarConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this); // Starlight
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreatePopOutableWindow<RadarConsoleWindow>(EntMan); // Starlight: popout
        _window.RadarClicked += OnRadarClicked; // Starlight: go to location for AI
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not NavBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(cState.State, cState.DockingPortStates); // Starlight: +DockStates
    }

    // Starlight: close popout
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Starlight - start
            if (_window != null)
                _window.RadarClicked -= OnRadarClicked;
            // Starlight - end
            _window?.DisposePopOut();
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
    #endregion
}
