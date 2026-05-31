using Content.Client.Shuttles;
using Content.Shared.Shuttles.Components;
using Robust.Client.Input;
using Robust.Client.Player;

namespace Content.Client.PAI;

/// <summary>
/// Safety net: if the shuttle console is deleted while the local player is still piloting it,
/// the server-side RemovePilot may bail early once the console component is tearing down,
/// leaving PilotComponent on the PAI and the input stuck in "shuttle" mode.
/// This system resets the input context on the client side so the screen never stays frozen.
/// </summary>
public sealed class PAIShuttleConsoleSystem : EntitySystem
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
    }

    #region Console Shutdown
    private void OnConsoleShutdown(EntityUid uid, ShuttleConsoleComponent component, ComponentShutdown args)
    {
        var localEntity = _playerManager.LocalEntity;
        if (localEntity == null)
            return;
        if (!TryComp<PilotComponent>(localEntity.Value, out var pilot))
            return;
        if (pilot.Console != uid)
            return;

        _input.Contexts.SetActiveContext("human");
    }
    #endregion
}
