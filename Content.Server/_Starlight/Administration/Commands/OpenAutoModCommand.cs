using Content.Server.Administration.UI;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.AutoMod)]
public sealed class OpenAutoModCommand : IConsoleCommand
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public string Command => "automod";
    public string Description => Loc.GetString("automod-command-description");
    public string Help => Loc.GetString("automod-command-help");

    public OpenAutoModCommand()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteLine(Loc.GetString("automod-command-no-server-console"));
            return;
        }

        var ui = new AutoModEui();
        _euiManager.OpenEui(ui, shell.Player);
    }
}