using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Arcade.Lancer;

/// <summary>
/// Monte Carlo balance evaluator for Lancer arcade missions.
/// Usage: lancer_balance [missionId|all] [trials] [hull] [agility] [engineering]
/// Targets (at Hull 2 / 0 Agility / 0 Engineering, best loadout): ridge-pass ~75%, deep-range ~50%, crown-signal ~25%.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class LancerBalanceCommand : IConsoleCommand
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public string Command => "lancer_balance";
    public string Description => "Monte Carlo Lancer arcade mission win-rate evaluator (eliminate-all, ignore objectives).";
    public string Help => "lancer_balance [missionId|all] [trials=500] [hull=2] [agility=0] [engineering=0]";

    private static readonly string[] MissionOrder = ["ridge-pass", "deep-range", "crown-signal"];
    private static readonly Dictionary<string, double> Targets = new()
    {
        ["ridge-pass"] = 0.75,
        ["deep-range"] = 0.50,
        ["crown-signal"] = 0.25,
    };

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var missionFilter = args.Length > 0 ? args[0] : "all";
        var trials = 500;
        var hull = 2;
        var agility = 0;
        var engineering = 0;

        if (args.Length > 1 && !int.TryParse(args[1], out trials))
        {
            shell.WriteError("trials must be an integer");
            return;
        }

        if (args.Length > 2 && !int.TryParse(args[2], out hull))
        {
            shell.WriteError("hull must be an integer");
            return;
        }

        if (args.Length > 3 && !int.TryParse(args[3], out agility))
        {
            shell.WriteError("agility must be an integer");
            return;
        }

        if (args.Length > 4 && !int.TryParse(args[4], out engineering))
        {
            shell.WriteError("engineering must be an integer");
            return;
        }

        trials = Math.Clamp(trials, 1, 10000);
        var sim = new LancerCombatSimulator(_prototypes);

        var missions = missionFilter.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? MissionOrder
            : new[] { missionFilter };

        shell.WriteLine($"Lancer balance — trials={trials} skills=H{hull}/A{agility}/E{engineering}");
        shell.WriteLine("(Win = clear all 3 fights as eliminate-all; narrative bonuses skipped; best loadout vs target.)");

        foreach (var missionId in missions)
        {
            if (!LancerGame.MissionLoadoutPairs.TryGetValue(missionId, out var loadouts))
            {
                shell.WriteError($"Unknown mission or no loadout pair: {missionId}");
                continue;
            }

            if (!_prototypes.HasIndex<Content.Shared._Starlight.Arcade.Lancer.LancerMissionPrototype>(missionId))
            {
                shell.WriteError($"Mission prototype missing: {missionId}");
                continue;
            }

            LancerCombatSimulator.BalanceResult? best = null;
            foreach (var loadoutId in loadouts)
            {
                var result = sim.EvaluateMission(missionId, loadoutId, trials, hull, agility, engineering);
                shell.WriteLine(
                    $"  {missionId} / {loadoutId}: {result.Wins}/{result.Trials} = {result.WinRate:P1}");

                if (best == null || result.WinRate > best.WinRate)
                    best = result;
            }

            if (best != null && Targets.TryGetValue(missionId, out var target))
            {
                var delta = best.WinRate - target;
                shell.WriteLine(
                    $"  >> best={best.LoadoutId} {best.WinRate:P1} (target {target:P0}, delta {delta:+0.0%;-0.0%})");
            }
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = MissionOrder.Append("all").Select(m => new CompletionOption(m));
            return CompletionResult.FromHintOptions(options, "missionId|all");
        }

        return args.Length switch
        {
            2 => CompletionResult.FromHint("trials"),
            3 => CompletionResult.FromHint("hull"),
            4 => CompletionResult.FromHint("agility"),
            5 => CompletionResult.FromHint("engineering"),
            _ => CompletionResult.Empty
        };
    }
}
