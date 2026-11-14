using Robust.Shared.Console;

namespace Content.Client._Starlight.Replay;

public sealed partial class ReplayGotoCommand : LocalizedEntityCommands
{
    [Dependency] private ReplayObserverSystem _observer = default!;

    public override string Command => "replay_goto";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (_observer.TryJumpToRoundTime(args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}
