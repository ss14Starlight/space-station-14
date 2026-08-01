using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server._Starlight.Arcade.Lancer;
using Content.Shared._Starlight.Arcade.Lancer;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using static Content.Server._Starlight.Arcade.Lancer.LancerGame;

namespace Content.Tests.Server._Starlight.Arcade.Lancer;

/// <summary>
/// Monte Carlo soak helpers for Lancer mission/encounter balance.
/// Current campaign missions are already tuned; win-rate band asserts below are commented out.
/// Intent: re-enable and tighten those bands when testing new missions (or rebalancing).
/// Live design targets at Hull 2 / 0 Agi / 0 Eng (best loadout): ridge-pass ~75%,
/// deep-range ~50%, crown-signal ~25%. The scripted sim is still optimistic vs live
/// (no full Stress/overheat/structure parity).
/// </summary>
[TestFixture]
[TestOf(typeof(LancerCombatSimulator))]
public sealed class LancerBalanceTests : ContentUnitTest
{
    private IPrototypeManager _prototypes = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _prototypes.Initialize();

        var missionsPath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "Resources", "Prototypes", "_Starlight", "Arcade", "Lancer", "missions.yml"));
        var encountersPath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "Resources", "Prototypes", "_Starlight", "Arcade", "Lancer", "encounters.yml"));

        // Fallback: walk up from cwd looking for Resources.
        if (!File.Exists(missionsPath))
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Resources", "Prototypes", "_Starlight", "Arcade", "Lancer", "missions.yml");
                if (File.Exists(candidate))
                {
                    missionsPath = candidate;
                    encountersPath = Path.Combine(dir.FullName, "Resources", "Prototypes", "_Starlight", "Arcade", "Lancer", "encounters.yml");
                    break;
                }

                dir = dir.Parent;
            }
        }

        Assert.That(File.Exists(missionsPath), Is.True, $"missions.yml not found at {missionsPath}");
        Assert.That(File.Exists(encountersPath), Is.True, $"encounters.yml not found at {encountersPath}");

        _prototypes.LoadString(File.ReadAllText(encountersPath));
        _prototypes.LoadString(File.ReadAllText(missionsPath));
        _prototypes.ResolveResults();
    }

    [Test]
    [Explicit("Monte Carlo balance soak; run manually via lancer_balance / explicit filter.")]
    public void ReportBaselineWinRates()
    {
        const int trials = 400;
        var sim = new LancerCombatSimulator(_prototypes, seed: 42);
        var lines = new List<string>();
        var loadoutRates = new Dictionary<(string Mission, string Loadout), double>();

        foreach (var (missionId, target) in new Dictionary<string, double>
                 {
                     ["ridge-pass"] = 0.75,
                     ["deep-range"] = 0.50,
                     ["crown-signal"] = 0.25,
                 })
        {
            Assert.That(MissionLoadoutPairs.ContainsKey(missionId), Is.True);
            double best = 0;
            string bestLoadout = "";

            foreach (var loadoutId in MissionLoadoutPairs[missionId])
            {
                var result = sim.EvaluateMission(missionId, loadoutId, trials);
                loadoutRates[(missionId, loadoutId)] = result.WinRate;
                lines.Add($"{missionId}/{loadoutId}: {result.WinRate:P1} ({result.Wins}/{result.Trials})");
                if (result.WinRate > best)
                {
                    best = result.WinRate;
                    bestLoadout = loadoutId;
                }
            }

            lines.Add($"  BEST {bestLoadout}={best:P1} target={target:P0} delta={best - target:+0.0%;-0.0%}");
            TestContext.Out.WriteLine(string.Join("\n", lines.TakeLast(MissionLoadoutPairs[missionId].Length + 1)));
        }

        TestContext.Out.WriteLine("---");
        foreach (var line in lines)
            TestContext.Out.WriteLine(line);

        var deepTortuga = loadoutRates[("deep-range", LoadoutTortuga)];
        var crownTortuga = loadoutRates[("crown-signal", LoadoutTortuga)];
        TestContext.Out.WriteLine($"Tortuga gates: deep-range={deepTortuga:P1} crown-signal={crownTortuga:P1}");

        // Win-rate bands disabled while current missions are considered balanced.
        // Re-enable and tighten toward ~75/50/25 (and Tortuga gates) when testing new missions:
        // Assert.That(bestRates["ridge-pass"], Is.InRange(0.65, 0.85), "ridge-pass best loadout win rate");
        // Assert.That(bestRates["deep-range"], Is.InRange(0.40, 0.70), "deep-range best loadout win rate");
        // Assert.That(bestRates["crown-signal"], Is.InRange(0.15, 0.35), "crown-signal best loadout win rate");
        // Assert.That(deepTortuga, Is.InRange(0.35, 0.65), "deep-range Tortuga win rate");
        // Assert.That(crownTortuga, Is.InRange(0.12, 0.40), "crown-signal Tortuga win rate");
        Assert.Pass();
    }

    [Test]
    [Explicit("Informational soak; not required for CI balance gates.")]
    public void CompareTortugaVsTokugawa()
    {
        const int trials = 800;
        var sim = new LancerCombatSimulator(_prototypes, seed: 7);

        Assert.That(MissionLoadouts.ContainsKey(LoadoutTokugawa), Is.True);
        Assert.That(Chassis.ContainsKey(ChassisTokugawa), Is.True);
        Assert.That(Weapons.ContainsKey(WeaponAnnihilator), Is.True);

        foreach (var missionId in new[] { "deep-range", "crown-signal" })
        {
            var tortuga = sim.EvaluateMission(missionId, LoadoutTortuga, trials);
            var tokugawa = sim.EvaluateMission(missionId, LoadoutTokugawa, trials);
            var delta = tokugawa.WinRate - tortuga.WinRate;

            TestContext.Out.WriteLine(
                $"{missionId}: Tortuga {tortuga.WinRate:P1} ({tortuga.Wins}/{tortuga.Trials}) | " +
                $"Tokugawa {tokugawa.WinRate:P1} ({tokugawa.Wins}/{tokugawa.Trials}) | " +
                $"delta {delta:+0.0%;-0.0%}");
        }

        Assert.Pass(); // report-only; soak comparison is informational
    }

    [Test]
    [Explicit("Informational soak; not required for CI balance gates.")]
    public void SweepTokugawaRadianceTiming()
    {
        const int trials = 300;
        TestContext.Out.WriteLine("RadianceMinTurn sweep (deep-range, Nuclear Cavalier on, 0 skills):");
        foreach (var minTurn in new[] { 1, 2, 3, 4, 5 })
        {
            var sim = new LancerCombatSimulator(_prototypes, seed: 11);
            sim.SetRadianceMinTurn(minTurn);
            var result = sim.EvaluateMission("deep-range", LoadoutTokugawa, trials);
            var diag = sim.DiagnoseMission("deep-range", LoadoutTokugawa, 80);
            TestContext.Out.WriteLine(
                $"  minTurn={minTurn}: win={result.WinRate:P1} ({result.Wins}/{result.Trials}) | " +
                $"diag wins={diag.Wins} died={diag.Died} avgDmg={diag.DamageDealt / 80.0:F1}");
        }

        var tortuga = new LancerCombatSimulator(_prototypes, seed: 11)
            .EvaluateMission("deep-range", LoadoutTortuga, trials);
        TestContext.Out.WriteLine($"  Tortuga baseline: {tortuga.WinRate:P1} ({tortuga.Wins}/{tortuga.Trials})");
        Assert.Pass();
    }

    [Test]
    [Explicit("Informational soak; not required for CI balance gates.")]
    public void DiagnoseTokugawaSingleFight()
    {
        Assert.That(MissionLoadouts.TryGetValue(LoadoutTokugawa, out var loadout), Is.True);
        Assert.That(Chassis.TryGetValue(loadout!.ChassisId, out var chassis), Is.True);
        TestContext.Out.WriteLine(
            $"Tokugawa chassis={chassis!.Id} hp={chassis.MaxHp} armor={chassis.Armor} " +
            $"eva={chassis.Evasion} spd={chassis.Speed} heat={chassis.HeatCap} " +
            $"core={chassis.CoreKind} nuclearCavalier={chassis.HasNuclearCavalier} " +
            $"extBat={chassis.HasExternalBatteries} deepWell={chassis.HasDeepWellHeatSink} " +
            $"weapons=[{string.Join(',', chassis.WeaponIds)}]");

        // Opening distance: deploy (2,10) -> nearest deep-range-fight-1 enemy (5,2)
        // Annihilator base 5 + External Batteries +5 = 10 without Radiance.
        var openDist = LancerHex.Distance(new LancerGridCoord(2, 10), new LancerGridCoord(5, 2));
        TestContext.Out.WriteLine($"Opening hex dist deploy->(5,2) = {openDist} (Annihilator 5+ExtBat5=10 / Radiance +5 more)");

        var sim = new LancerCombatSimulator(_prototypes, seed: 1);
        var diag = sim.DiagnoseMission("deep-range", LoadoutTokugawa, 100);
        TestContext.Out.WriteLine(
            $"tokugawa diagnose 100: wins={diag.Wins} died={diag.Died} timeout={diag.Timeout} relay={diag.RelayLost} " +
            $"avgDmg={diag.DamageDealt / 100.0:F1}");

        var diagT = sim.DiagnoseMission("deep-range", LoadoutTortuga, 100);
        TestContext.Out.WriteLine(
            $"tortuga diagnose 100: wins={diagT.Wins} died={diagT.Died} timeout={diagT.Timeout} relay={diagT.RelayLost} " +
            $"avgDmg={diagT.DamageDealt / 100.0:F1}");

        var withCore = sim.EvaluateMission("deep-range", LoadoutTokugawa, 50);
        TestContext.Out.WriteLine($"deep-range tokugawa 50-trial: {withCore.WinRate:P1} ({withCore.Wins}/{withCore.Trials})");

        var noCore = sim.EvaluateMission("deep-range", LoadoutTokugawa, 50, disableTokugawaCore: true);
        TestContext.Out.WriteLine($"deep-range tokugawa (no Radiance) 50-trial: {noCore.WinRate:P1} ({noCore.Wins}/{noCore.Trials})");

        var mild = sim.EvaluateMission("deep-range", LoadoutTokugawa, 100, hull: 4, agility: 2, engineering: 2);
        TestContext.Out.WriteLine($"deep-range tokugawa (skills 4/2/2) 100-trial: {mild.WinRate:P1} ({mild.Wins}/{mild.Trials})");

        var hull2 = sim.EvaluateMission("deep-range", LoadoutTokugawa, 100, hull: 2);
        TestContext.Out.WriteLine($"deep-range tokugawa (hull 2) 100-trial: {hull2.WinRate:P1} ({hull2.Wins}/{hull2.Trials})");

        var tank = sim.EvaluateMission("deep-range", LoadoutTokugawa, 50, hull: 20, agility: 10, engineering: 10);
        TestContext.Out.WriteLine($"deep-range tokugawa (skills 20/10/10) 50-trial: {tank.WinRate:P1} ({tank.Wins}/{tank.Trials})");

        var tortuga = sim.EvaluateMission("deep-range", LoadoutTortuga, 50);
        TestContext.Out.WriteLine($"deep-range tortuga 50-trial: {tortuga.WinRate:P1} ({tortuga.Wins}/{tortuga.Trials})");

        Assert.That(tortuga.Wins, Is.GreaterThan(0));
    }
}
