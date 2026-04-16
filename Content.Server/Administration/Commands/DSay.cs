using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Robust.Shared.Console;
using Content.Server.Administration.Managers; // Starlight
using Content.Shared.Ghost; // Starlight

namespace Content.Server.Administration.Commands;

[AnyCommand]
public sealed class DsayCommand : LocalizedEntityCommands
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IAdminManager _admin = default!; // Starlight

    public override string Command => "dsay";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (player.AttachedEntity is not { Valid: true } entity)
        {
            shell.WriteError(Loc.GetString("shell-must-be-attached-to-entity"));
            return;
        }

        // Starlight begin: Check if ghost or admin here instead of on the client. Both for ghost admeme reasons but also because trusting client bad.
        if (!EntityManager.HasComponent<GhostComponent>(entity) && !_admin.HasAdminFlag(player, AdminFlags.Admin))
        {
            shell.WriteError("Tried to speak on deadchat without being ghost or admin.");
            return;
        }
        // Starlight end

        if (args.Length < 1)
            return;

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        _chatSystem.TrySendInGameOOCMessage(entity, message, InGameOOCChatType.Dead, false, shell, player);
    }
}
