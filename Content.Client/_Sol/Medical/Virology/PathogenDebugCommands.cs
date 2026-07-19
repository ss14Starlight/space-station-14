using Content.Client._Sol.Medical.Virology;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Client._Sol.Medical.Virology;

[UsedImplicitly]
public sealed class PathogenOverlayModeCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public override string Command => "ptvmode";
    public override string Help => "ptvmode <TotalLoad|SpecificPathogen> [pathogenId]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!Enum.TryParse<PathogenDebugOverlayMode>(args[0], out var mode))
        {
            shell.WriteError("Invalid mode.");
            return;
        }

        var sys = _systems.GetEntitySystem<PathogenDebugOverlaySystem>();
        sys.CfgMode = mode;
        if (mode == PathogenDebugOverlayMode.SpecificPathogen)
        {
            if (args.Length != 2)
            {
                shell.WriteError("SpecificPathogen requires a pathogen prototype id.");
                return;
            }

            sys.CfgSpecificPathogen = args[1];
        }
    }
}

[UsedImplicitly]
public sealed class PathogenOverlayRangeCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public override string Command => "ptvrange";
    public override string Help => "ptvrange <start> <end>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 ||
            !float.TryParse(args[0], out var start) ||
            !float.TryParse(args[1], out var end) ||
            start == end)
        {
            shell.WriteLine(Help);
            return;
        }

        var sys = _systems.GetEntitySystem<PathogenDebugOverlaySystem>();
        sys.CfgBase = start;
        sys.CfgScale = end - start;
    }
}

[UsedImplicitly]
public sealed class PathogenOverlayCbmCommand : LocalizedCommands
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public override string Command => "ptvcbm";
    public override string Help => "ptvcbm <true|false>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !bool.TryParse(args[0], out var flag))
        {
            shell.WriteLine(Help);
            return;
        }

        _systems.GetEntitySystem<PathogenDebugOverlaySystem>().CfgCBM = flag;
    }
}
