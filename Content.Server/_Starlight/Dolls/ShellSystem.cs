using Content.Shared.Actions;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared.Alert;

namespace Content.Server._Starlight.Dolls;

public sealed partial class ShellSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShellComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShellComponent, ComponentShutdown>(OnCompRemove);
    }

    private void OnMapInit(EntityUid uid, ShellComponent comp, MapInitEvent args)
    {
        _actionsSystem.AddAction(uid, ref comp.GenerateShellPieceActionEntity, comp.GenerateShellPieceAction);
        _alerts.ShowAlert(uid, comp.ShellAlert);
    }

    private void OnCompRemove(EntityUid uid, ShellComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.GenerateShellPieceActionEntity);
        _alerts.ClearAlert(uid, comp.ShellAlert);
    }
}
