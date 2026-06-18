using Content.Shared._Starlight.ViewVariables;
using Robust.Client.Console;

namespace Content.Client._Starlight.ViewVariables;

/// <summary>
/// Currently serves the sole purpose of listening for OpenViewVariablesEvent. Riviting I know.
/// </summary>
public sealed partial class ClientViewVariablesSystem : EntitySystem
{
    [Dependency] private IClientConsoleHost _shell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<OpenViewVariablesEvent>(OnOpenViewVariables);
    }

    private void OnOpenViewVariables(OpenViewVariablesEvent ev) => _shell.ExecuteCommand($"vv {ev.Path}");
}
