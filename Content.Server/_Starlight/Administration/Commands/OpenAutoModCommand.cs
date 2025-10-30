using Content.Server.Administration.UI;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.AutoMod)]
    public sealed class OpenAutoModCommand : IConsoleCommand
    {
        public string Command => "automod";
        public string Description => "Opens the admin auto mod panel.";
        public string Help => "Usage: automod";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine("This does not work from the server console.");
                return;
            }

            var eui = IoCManager.Resolve<EuiManager>();
            var ui = new AutoModEui();
            eui.OpenEui(ui, player);
        }
    }
}
