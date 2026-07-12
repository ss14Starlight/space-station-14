using Content.Shared._Starlight.ViewVariables;
using Robust.Client.Console;
using Robust.Client.ViewVariables;
using Robust.Shared.Localization;

namespace Content.Client._Starlight.ViewVariables;

/// <summary>
/// Currently serves the sole purpose of listening for OpenViewVariablesEvent. Riviting I know.
/// </summary>
public sealed partial class ClientViewVariablesSystem : EntitySystem
{
    [Dependency] private IClientConsoleHost _shell = default!;
    [Dependency] private IViewVariableControlFactory _vvFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        _vvFactory.RegisterForType<LocId>(_ => new VVPropEditorLocId());

        SubscribeNetworkEvent<OpenViewVariablesEvent>(OnOpenViewVariables);
    }

    private void OnOpenViewVariables(OpenViewVariablesEvent ev) => _shell.ExecuteCommand($"vv {ev.Path}");
}
