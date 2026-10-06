using System.Buffers;
using System.Globalization;
using System.Text;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.GameTicking;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server._Starlight.Statistics;

/// <summary>
/// Collects round-scoped gameplay statistics and emits structured Loki records.
/// </summary>
public sealed partial class RoundStatisticsSystem : EntitySystem
{
    /// <summary>
    /// Every record starts with this, so a Loki query can select them with a line filter.
    /// </summary>
    public const string RecordPrefix = "sl_round_stat";

    private const string UnknownFork = "unknown";

    /// <summary>
    /// Upper bound on the records kept in <see cref="Records"/>, well past what a round emits.
    /// </summary>
    private const int MaxRetainedRecords = 4096;

    private static readonly SearchValues<char> _quoteRequired = SearchValues.Create(" \t\r\n\"=");

    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("round.statistics");
    private readonly List<Action<RoundEndMessageEvent>> _statisticsEmitters = [];
    private readonly List<Action> _statisticsClearers = [];
    private readonly List<string> _records = [];

    /// <summary>
    /// The current round's records, as written to the log, kept until the round is cleaned up.
    /// </summary>
    [ViewVariables]
    public IReadOnlyList<string> Records => _records;

    private string _forkId = UnknownFork;
    private int? _roundId;
    private bool _roundLogged;

    public override void Initialize()
    {
        base.Initialize();

        var forkId = _configuration.GetCVar(CVars.BuildForkId);
        _forkId = string.IsNullOrWhiteSpace(forkId) ? UnknownFork : forkId;

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        InitializeRoundStatistics();
        InitializeJobStatistics();
        InitializeAntagStatistics();
        InitializeObjectiveStatistics();
        InitializeDynamicStatistics();
        InitializeStoreStatistics();
        InitializeSecureTerminalStatistics();
        InitializeGhostRoleStatistics();
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        if (_roundId == args.Id)
            return;

        ClearStatistics();
        _roundId = args.Id;
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        if (_roundId != args.RoundId || _roundLogged)
            return;

        _roundLogged = true;

        foreach (var emitStatistics in _statisticsEmitters)
        {
            emitStatistics(args);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        ClearStatistics();
        _roundId = null;
    }

    private bool EnsureRound()
    {
        var roundId = _gameTicker.RoundId;
        if (roundId <= 0)
            return false;

        if (_roundId == roundId)
            return true;

        ClearStatistics();
        _roundId = roundId;
        return true;
    }

    private void RegisterStatisticsDomain(Action<RoundEndMessageEvent> emitStatistics, Action clearStatistics)
    {
        _statisticsEmitters.Add(emitStatistics);
        _statisticsClearers.Add(clearStatistics);
    }

    private void EmitRoundRecord(int roundId, string message, params object?[] args)
    {
        var record = string.Concat(
            RecordPrefix,
            " schema=1 fork=",
            FormatValue(_forkId),
            " round_id=",
            FormatValue(roundId),
            " ",
            RenderTemplate(message, args));

        _sawmill.Info(record);

        if (_records.Count < MaxRetainedRecords)
            _records.Add(record);
    }

    /// <summary>
    /// Substitutes each <c>{Name}</c> hole in a record template with the next formatted value.
    /// </summary>
    private static string RenderTemplate(string message, object?[] args)
    {
        var builder = new StringBuilder(message.Length + (args.Length * 8));
        var index = 0;
        var position = 0;

        while (position < message.Length)
        {
            var open = message.IndexOf('{', position);
            if (open < 0)
                break;

            var close = message.IndexOf('}', open);
            if (close < 0)
                break;

            builder.Append(message, position, open - position);
            builder.Append(index < args.Length ? FormatValue(args[index]) : string.Empty);

            index++;
            position = close + 1;
        }

        builder.Append(message, position, message.Length - position);
        return builder.ToString();
    }

    /// <summary>
    /// Formats a single logfmt value.
    /// </summary>
    private static string FormatValue(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            string str => str,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        if (text.Length > 0 && !text.AsSpan().ContainsAny(_quoteRequired))
            return text;

        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        return string.Concat("\"", escaped, "\"");
    }

    private void EmitCurrentRoundRecord(string message, params object?[] args)
    {
        var roundId = _gameTicker.RoundId;
        if (roundId <= 0)
            return;

        EmitRoundRecord(roundId, message, args);
    }

    private void ClearStatistics()
    {
        foreach (var clearStatistics in _statisticsClearers)
        {
            clearStatistics();
        }

        _records.Clear();
        _roundLogged = false;
    }

    private static void Increment<TKey>(Dictionary<TKey, int> statistics, TKey key) where TKey : notnull => statistics[key] = statistics.GetValueOrDefault(key) + 1;

    private static void Add<TKey>(Dictionary<TKey, double> statistics, TKey key, double value) where TKey : notnull => statistics[key] = statistics.GetValueOrDefault(key) + value;

    /// <summary>
    /// Formats a number for a logfmt field, never emitting a decimal comma LogQL cannot unwrap.
    /// </summary>
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
