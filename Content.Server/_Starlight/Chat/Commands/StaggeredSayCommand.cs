using Content.Server.Chat.Commands;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Chat.Commands;

/// <summary>
/// Variant of say command, but enforces a cooldown before it can be executed again by the executing player.
/// </summary>
/// <remarks>
/// Created mostly to curb people being incapable of using their eyeballs to read and spamming the cmdlink that I have
/// in my (neomoth) aghost's description as a bit.
/// </remarks>
[AnyCommand]
internal sealed partial class StaggeredSayCommand : SayCommand
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IConfigurationManager _config = null!;

    private readonly Dictionary<ICommonSession, TimeSpan> _cooldowns = [];

    public override string Command => "ssay";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is null)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (_cooldowns.TryGetValue(shell.Player, out var cooldown))
        {
            if (_timing.CurTime < cooldown)
            {
                shell.WriteLine(
                    $"Unable to use this command again for {_cooldowns[shell.Player].TotalSeconds - _timing.CurTime.TotalSeconds:F2} seconds.");
                return;
            }
            _cooldowns.Remove(shell.Player);
        }

        base.Execute(shell, argStr, args);
        _cooldowns.Add(shell.Player,
            _timing.CurTime + TimeSpan.FromSeconds(_config.GetCVar(StarlightCCVars.StaggeredSayCooldown)));
    }
}
