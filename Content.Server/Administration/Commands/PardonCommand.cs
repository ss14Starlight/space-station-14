using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Ban)]
    public sealed class PardonCommand : LocalizedCommands
    {
        //[Dependency] private readonly IServerDbManager _dbManager = default!; NullLink-edit: move to general method at Manager
        [Dependency] private readonly IBanManager _banManager = default!;

        public override string Command => "pardon";

        public override async void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;

            if (args.Length != 1)
            {
                shell.WriteLine(Help);
                return;
            }

            if (!int.TryParse(args[0], out var banId))
            {
                shell.WriteLine(Loc.GetString($"cmd-pardon-unable-to-parse", ("id", args[0]), ("help", Help)));
                return;
            }

            var ban = await _banManager.GetServerBanAsync(banId); // NullLink-edit: move to general method at Manager

            if (ban == null)
            {
                shell.WriteLine($"No ban found with id {banId}");
                return;
            }

            if (ban.Unban != null)
            {
                if (ban.Unban.UnbanningAdmin != null)
                {
                    shell.WriteLine(Loc.GetString($"cmd-pardon-already-pardoned-specific",
                        ("admin", ban.Unban.UnbanningAdmin.Value),
                        ("time", ban.Unban.UnbanTime)));
                }

                else
                    shell.WriteLine(Loc.GetString($"cmd-pardon-already-pardoned"));

                return;
            }

            await _banManager.CreateServerUnban(banId, player?.UserId, DateTimeOffset.Now); // NullLink-edit: move to general method at Manager

            shell.WriteLine(Loc.GetString($"cmd-pardon-success", ("id", banId)));
        }
    }
}
