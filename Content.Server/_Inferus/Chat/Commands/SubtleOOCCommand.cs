using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Inferus.Chat.Commands;

[AnyCommand]
public sealed class SubtleOOCCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public string Command => "sooc";
    public string Description => "Send a subtle LOOC message that only nearby people can see.";
    public string Help => "Usage: sooc <text>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("This command can only be run by a player.");
            return;
        }

        if (player.AttachedEntity is not { Valid: true } entity)
        {
            shell.WriteError("You don't have an entity!");
            return;
        }

        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        var chat = _entitySystemManager.GetEntitySystem<ChatSystem>();
        chat.TrySendSubtleOOC(entity, player, message);
    }
}
