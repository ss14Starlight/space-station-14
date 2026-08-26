using Content.Shared.Atmos.Components;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Atmos.Consoles;

public sealed class AtmosMonitoringConsoleBoundUserInterface : BoundUserInterface
{
    #region Starlight
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    #endregion
    [ViewVariables]
    private AtmosMonitoringConsoleWindow? _menu;

    public AtmosMonitoringConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this); // Starlight: go to clicked position for AI
    }

    protected override void Open()
    {
        base.Open();

        _menu = new AtmosMonitoringConsoleWindow(this, Owner);
        _menu.OpenCentered();
        _menu.OnClose += Close;
    }

    // Starlight - start
    // go to clicked position for AI
    public void SendMapClicked(EntityCoordinates coordinates)
    {
        var local = _playerManager.LocalEntity;
        if (local is null || !EntMan.HasComponent<StationAiHeldComponent>(local.Value))
            return;

        SendMessage(new CrewMonitoringWarpRequestMessage(EntMan.GetNetCoordinates(coordinates)));
    }
    // Starlight - end
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AtmosMonitoringConsoleBoundInterfaceState castState)
            return;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        _menu?.UpdateUI(xform?.Coordinates, castState.AtmosNetworks);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Dispose();
    }
}
