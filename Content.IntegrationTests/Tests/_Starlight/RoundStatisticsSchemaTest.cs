#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.Statistics;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.SecureTerminal;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight;

/// <summary>
/// A typo in a record template breaks a Grafana dashboard without breaking the build, so this
/// asserts the logfmt shape of every record a real round emits.
/// </summary>
[TestFixture]
public sealed class RoundStatisticsSchemaTest : GameTest
{
    /// <summary>
    /// A logfmt field: a bare key, then either a quoted value or a value with no whitespace.
    /// </summary>
    private static readonly Regex _fieldPattern = new(
        """^[a-z_][a-z0-9_]*=(?:"(?:[^"\\]|\\.)*"|[^\s"=]*)$""",
        RegexOptions.Compiled);

    /// <summary>
    /// Splits a record on whitespace that is not inside a quoted value.
    /// </summary>
    private static readonly Regex _fieldSplitter = new(
        """\s+(?=(?:[^"]*"[^"]*")*[^"]*$)""",
        RegexOptions.Compiled);

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = PoolManager.TestStation
    };

    [Test]
    public async Task RecordsAreWellFormed()
    {
        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, false);
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, false);

        var server = Pair.Server;
        await server.WaitIdleAsync();

        var systems = server.ResolveDependency<IEntitySystemManager>();
        var gameTicker = systems.GetEntitySystem<GameTicker>();
        var statistics = systems.GetEntitySystem<RoundStatisticsSystem>();

        // A round that never touches the secure command terminal leaves its per-request record
        // unpopulated, so walk one proposal through its whole lifecycle first.
        await server.WaitPost(() =>
        {
            statistics.RecordSecureTerminalProposal("ErtSecurity", SecureTerminalActionType.GameRule, true, 5000);
            statistics.RecordSecureTerminalAuthorization("ErtSecurity", SecureTerminalActionType.GameRule, false);
            statistics.RecordSecureTerminalAuthorization("ErtSecurity", SecureTerminalActionType.GameRule, true);
            statistics.RecordSecureTerminalActivation("ErtSecurity", SecureTerminalActionType.GameRule, TimeSpan.FromSeconds(42), 0.05f);
            statistics.RecordSecureTerminalOutcome("ErtSecurity", SecureTerminalActionType.GameRule, SecureTerminalResult.Executed);
            statistics.RecordSecureTerminalRefund("ErtSecurity", SecureTerminalActionType.GameRule, 2500);
        });

        await server.WaitPost(() => gameTicker.EndRound());
        await server.WaitRunTicks(3);

        var records = statistics.Records.ToList();

        Assert.That(records, Is.Not.Empty, "A completed round emitted no statistics records.");

        var prefix = $"{RoundStatisticsSystem.RecordPrefix} schema=1 ";
        var kinds = new HashSet<string>();

        Assert.Multiple(() =>
        {
            foreach (var record in records)
            {
                Assert.That(record, Does.StartWith(prefix),
                    $"Record is missing the schema prefix: {record}");

                var fields = new Dictionary<string, string>();
                foreach (var field in _fieldSplitter.Split(record[prefix.Length..]))
                {
                    if (field.Length == 0)
                        continue;

                    Assert.That(_fieldPattern.IsMatch(field),
                        $"Field '{field}' is not valid logfmt, in record: {record}");

                    var split = field.IndexOf('=');
                    fields[field[..split]] = field[(split + 1)..];
                }

                Assert.That(fields.ContainsKey("round_id"), $"Record is missing round_id: {record}");
                Assert.That(fields.ContainsKey("kind"), $"Record is missing kind: {record}");

                if (fields.TryGetValue("kind", out var kind))
                    kinds.Add(kind);
            }
        });

        // A round that starts and ends always produces these, whatever else it rolled.
        Assert.That(kinds, Is.SupersetOf(new[]
            {
                "round_summary", "population", "secure_terminal", "secure_terminal_summary",
                "ghost_role_summary",
            }),
            $"Emitted kinds were: {string.Join(", ", kinds.Order())}");

        Server.CfgMan.SetCVar(StarlightCCVars.DisableLoadMapRule, true);
        Server.CfgMan.SetCVar(CCVars.GameRoleTimers, true);
    }
}
