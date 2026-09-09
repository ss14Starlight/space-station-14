using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Join the round on the tutorial server by opening the role picker (same path as ghosts / Choose a tutorial).
/// </summary>
[AnyCommand]
public sealed partial class JoinTutorialCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public string Command => "jointutorial";
    public string Description => "Join the tutorial server and open the role picker.";
    public string Help => "Usage: jointutorial";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
            return;

        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var ticker = _entManager.System<GameTicker>();
        var tutorial = _entManager.System<TutorialServerRuleSystem>();

        if (ticker.RunLevel == GameRunLevel.PreRoundLobby)
        {
            shell.WriteError(Loc.GetString("tutorial-server-join-round-not-started"));
            return;
        }

        if (ticker.PlayerGameStatuses.TryGetValue(player.UserId, out var status) &&
            status == PlayerGameStatus.JoinedGame)
        {
            shell.WriteError(Loc.GetString("tutorial-server-join-round-already-in-game"));
            return;
        }

        if (!tutorial.IsTutorialServerActive())
        {
            shell.WriteError(Loc.GetString("tutorial-server-join-round-not-tutorial"));
            return;
        }

        if (_adminManager.IsAdmin(player) && _cfg.GetCVar(CCVars.AdminDeadminOnJoin))
            _adminManager.DeAdmin(player);

        // No job id — PlayerBeforeSpawn opens the tutorial role picker.
        ticker.MakeJoinGame(player, EntityUid.Invalid);
    }
}
