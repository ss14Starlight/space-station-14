using Content.Server.Administration;
using Content.Server.Silicons.Laws;
using Content.Shared.Administration;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Silicons.Commands;

[AdminCommand(AdminFlags.Logs)]
public sealed partial class ListLawsCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;

    public override string Command => "lslaws";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        ICommonSession? player;
        if (args.Length > 0)
            _players.TryGetSessionByUsername(args[0], out player);
        else
            player = shell.Player;

        if (player == null)
        {
            shell.WriteError(LocalizationManager.GetString("shell-target-player-does-not-exist"));
            return;
        }

        if (player.AttachedEntity is not { } target)
        {
            shell.WriteError(LocalizationManager.GetString("shell-target-entity-does-not-have-message",
                ("missing", "entity")));
            return;
        }

        if (!_entities.TryGetComponent<SiliconLawBoundComponent>(target, out var lawBound))
        {
            shell.WriteError(LocalizationManager.GetString("shell-target-entity-does-not-have-message",
                ("missing", "SiliconLawBoundComponent")));
            return;
        }

        shell.WriteLine($"Laws for player {player.UserId}:");
        var lawSystem = _entities.System<SiliconLawSystem>();
        var lawset = lawSystem.GetLaws(target, lawBound);

        if (lawset.Laws.Count == 0)
        {
            shell.WriteLine("None.");
        }

        for (var i = 0; i < lawset.Laws.Count; i++)
        {
            var law = lawset.Laws[i];
            // Cleanly handle ion laws, they have yucky formatting problems that need to be avoided
            var rawId = law.LawIdentifierOverride ?? law.Order.ToString();
            var id = rawId.StartsWith('[') ? "?" : rawId;
            var text = LocalizationManager.TryGetString(law.LawString, out var loc)
                ? loc
                : law.LawString;
            shell.WriteLine($"- [{id}]: {text}");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), LocalizationManager.GetString("shell-argument-username-hint"));
        }

        return CompletionResult.Empty;
    }
}
