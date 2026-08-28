using Content.Shared.Actions;
using Content.Shared._Starlight.Actions.Components;

namespace Content.Server._Starlight.Actions.EntitySystems;

public sealed class ShellSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShellComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShellComponent, ComponentShutdown>(OnCompRemove);
    }

    private void OnMapInit(EntityUid uid, ShellComponent comp, MapInitEvent args)
    {
        _actionsSystem.AddAction(uid, ref comp.GenerateShellPieceActionEntity, comp.GenerateShellPieceAction);
    }

    private void OnCompRemove(EntityUid uid, ShellComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.GenerateShellPieceActionEntity);
    }
}
