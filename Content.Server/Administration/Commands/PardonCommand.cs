using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Ban)]
    public sealed partial class PardonCommand : LocalizedCommands
    {
        //[Dependency] private readonly IServerDbManager _dbManager = default!; NullLink-edit: move to general method at Manager
        [Dependency] private IBanManager _banManager = default!;
        [Dependency] private ILogManager _logManager = default!; // NullLink-edit

        public override string Command => "pardon";

        public override async void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            // Starlight-start: Move all code into internal method and use catch to catch errors
            try
            {
                await ExecuteInternal(shell, argStr, args);
            }
            catch (Exception e)
            {
                _logManager.GetSawmill("admin.bans").Error($"Pardon command failed: {e}");
            }
            // Starlight-end
        }

        public async Task ExecuteInternal(IConsoleShell shell, string argStr, string[] args) // Starlight-edit
        {
            var player = shell.Player;

            if (args.Length is < 1 or > 3) // NullLink-edit: Project and Server name optional parameters
            {
                shell.WriteLine(Help);
                return;
            }

            if (!int.TryParse(args[0], out var banId))
            {
                shell.WriteLine(Loc.GetString($"cmd-pardon-unable-to-parse", ("id", args[0]), ("help", Help)));
                return;
            }

            // NullLink-start: move to general method at Manager
            var ban = args.Length >= 3 && !string.IsNullOrWhiteSpace(args[1]) && !string.IsNullOrWhiteSpace(args[2])
                ? await _banManager.GetServerBanAsync(banId, args[1], args[2])
                : await _banManager.GetServerBanAsync(banId);
            // NullLink-end

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

            await _banManager.CreateServerUnban(banId, player?.UserId, DateTimeOffset.Now, ban.ProjectName, ban.ServerName); // NullLink-edit: move to general method at Manager

            shell.WriteLine(Loc.GetString($"cmd-pardon-success", ("id", banId)));
        }
    }
}
