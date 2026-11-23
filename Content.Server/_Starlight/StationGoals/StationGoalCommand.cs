using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.StationGoals;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StationGoals;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class StationGoalCommand : LocalizedEntityCommands
{
    [Dependency] private StationGoalSystem _goals = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "stationgoal";

    private static readonly string[] _subCommands = ["list", "refresh", "activate", "progress", "complete", "cancel", "vote"];

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError(Help);
            return;
        }

        var query = EntityManager.EntityQueryEnumerator<StationGoalsComponent>();
        if (!query.MoveNext(out var uid, out var comp))
        {
            shell.WriteError("No station has goals.");
            return;
        }

        var station = new Entity<StationGoalsComponent>(uid, comp);
        switch (args[0])
        {
            case "list":
                shell.WriteLine($"Offers: {string.Join(", ", comp.Offers)}");
                foreach (var active in comp.Active)
                    shell.WriteLine($"Active: {active.Goal} [{string.Join(", ", active.Progress.Select(p => p.Value))}]");
                shell.WriteLine($"Finished: {string.Join(", ", comp.Finished)}");
                break;
            case "refresh":
                _goals.RefreshOffers((station, station.Comp));
                shell.WriteLine($"Offers: {string.Join(", ", comp.Offers)}");
                break;
            case "vote":
                shell.WriteLine(_goals.TryStartVote(station) ? "Vote started." : "Could not start a vote.");
                break;
            case "activate" when args.Length >= 2:
                shell.WriteLine(_goals.TryActivate(station, args[1], force: true) ? "Activated." : "Could not activate.");
                break;
            case "progress" when args.Length >= 2:
                var value = args.Length >= 3 && float.TryParse(args[2], out var parsed) ? parsed : 1f;
                _goals.ReportProgress(uid, args[1], value);
                shell.WriteLine($"Reported {args[1]} = {value}.");
                break;
            case "complete" when args.Length >= 2:
            case "cancel" when args.Length >= 2:
                if (comp.Active.FirstOrDefault(a => a.Goal == args[1]) is not { } goal)
                {
                    shell.WriteError("That goal is not active.");
                    return;
                }

                if (args[0] == "complete")
                    _goals.CompleteGoal(station, goal);
                else
                    _goals.FailGoal(station, goal, announce: false);
                shell.WriteLine("Done.");
                break;
            default:
                shell.WriteError(Help);
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(_subCommands);

        if (args.Length == 2 && args[0] is "activate" or "complete" or "cancel")
            return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<StationGoalPrototype>(proto: _proto), "<goal>");

        if (args.Length == 2 && args[0] == "progress")
            return CompletionResult.FromHint("<key>");

        if (args.Length == 3 && args[0] == "progress")
            return CompletionResult.FromHint("<value>");

        return CompletionResult.Empty;
    }
}
