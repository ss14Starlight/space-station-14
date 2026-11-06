using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared._NullLink;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Chat.Commands;

[AnyCommand]
internal sealed partial class MentorChatCommand : LocalizedCommands
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private ISharedNullLinkPlayerRolesReqManager _reqManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;

    public override string Command => "msay";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;

        if (player == null)
        {
            shell.WriteError(Loc.GetString($"shell-cannot-run-command-from-server"));
            return;
        }

        if (!_reqManager.IsMentor(player) && !_adminManager.IsAdmin(player))
        {
            shell.WriteError("Not a mentor");
            return;
        }

        if (args.Length < 1)
            return;

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        _chatManager.TrySendOOCMessage(player, message, OOCChatType.Mentor);
    }
}
