using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Physics.Diagnostics;

/// <summary>
/// Reports where physics tick time goes over a few seconds. See <see cref="PhysicsReportSystem"/>.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class PhysicsReportCommand : LocalizedEntityCommands
{
    [Dependency] private PhysicsReportSystem _report = default!;

    private const float DefaultSeconds = 10f;
    private const float MaxSeconds = 120f;

    public override string Command => "physicsreport";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var seconds = DefaultSeconds;
        if (args.Length > 1
            || (args.Length == 1 && !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            || seconds <= 0f
            || seconds > MaxSeconds)
        {
            shell.WriteError(Help);
            return;
        }

        if (!_report.TryStart(shell, seconds))
        {
            shell.WriteError(Loc.GetString("cmd-physicsreport-running"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-physicsreport-started", ("seconds", seconds)));
    }
}
