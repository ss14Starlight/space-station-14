using Content.Shared.Atmos.Components;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Atmos.Consoles;

public sealed class AtmosAlertsComputerBoundUserInterface : BoundUserInterface
{
    #region Starlight
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    #endregion
    [ViewVariables]
    private AtmosAlertsComputerWindow? _menu;

    public AtmosAlertsComputerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this); // Starlight: go to clicked location for AI
    }

    protected override void Open()
    {
        base.Open();

        _menu = new AtmosAlertsComputerWindow(this, Owner);
        _menu.OpenCentered();
        _menu.OnClose += Close;
    }

    #region Starlight
    /// <summary>
    /// Sends a map-click request to move the Station AI's remote eye to the specified coordinates.
    /// </summary>
    /// <param name="coordinates">The map coordinates selected by the user.</param>
    public void SendMapClicked(EntityCoordinates coordinates)
    {
        var local = _playerManager.LocalEntity;
        if (local is null || !EntMan.HasComponent<StationAiHeldComponent>(local.Value))
            return;

        SendMessage(new CrewMonitoringWarpRequestMessage(EntMan.GetNetCoordinates(coordinates)));
    }
    #endregion

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var castState = (AtmosAlertsComputerBoundInterfaceState) state;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        _menu?.UpdateUI(xform?.Coordinates, castState.AirAlarms, castState.FireAlarms, castState.FocusData);
    }

    public void SendFocusChangeMessage(NetEntity? netEntity)
    {
        SendMessage(new AtmosAlertsComputerFocusChangeMessage(netEntity));
    }

    public void SendDeviceSilencedMessage(NetEntity netEntity, bool silenceDevice)
    {
        SendMessage(new AtmosAlertsComputerDeviceSilencedMessage(netEntity, silenceDevice));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Dispose();
    }
}
