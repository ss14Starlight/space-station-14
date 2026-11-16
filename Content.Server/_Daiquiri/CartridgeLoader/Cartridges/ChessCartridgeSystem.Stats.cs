using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared.GameTicking;

namespace Content.Server._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// One row of the public chess leaderboard.
/// </summary>
public sealed record ChessLeaderboardEntry(
    string Name,
    string DisplayName,
    int Wins,
    int Losses,
    int Draws,
    int Games,
    double WinRate,
    DateTimeOffset LastPlayedUtc);

public sealed partial class ChessCartridgeSystem
{
    private const string LeaderboardFileName = "chess_leaderboard.json";

    private readonly Dictionary<string, ChessPlayerStats> _chessStats = new();
    private readonly object _chessStatsLock = new();
    private bool _chessStatsLoaded;

    /// <summary>
    /// Updates the in-memory win/loss/draw counters for both SS14 players. Lichess-only
    /// opponents (random humans and the local bot) have no SS14 session name and are
    /// intentionally not added to the leaderboard.
    /// </summary>
    private void RecordChessStats(
        ChessPlayerResult white,
        ChessPlayerResult black,
        ChessGameOutcome outcome)
    {
        lock (_chessStatsLock)
        {
            EnsureChessStatsLoadedLocked();

            if (!string.IsNullOrWhiteSpace(white.SessionName))
            {
                RecordChessPlayerLocked(
                    white,
                    outcome == ChessGameOutcome.WhiteWin
                        ? ChessResultKind.Win
                        : outcome == ChessGameOutcome.BlackWin
                            ? ChessResultKind.Loss
                            : ChessResultKind.Draw);
            }

            if (!string.IsNullOrWhiteSpace(black.SessionName))
            {
                RecordChessPlayerLocked(
                    black,
                    outcome == ChessGameOutcome.BlackWin
                        ? ChessResultKind.Win
                        : outcome == ChessGameOutcome.WhiteWin
                            ? ChessResultKind.Loss
                            : ChessResultKind.Draw);
            }

            SaveChessStatsLocked();
        }
    }

    private void RecordChessPlayerLocked(ChessPlayerResult player, ChessResultKind result)
    {
        var key = player.SessionName.Trim();
        if (!_chessStats.TryGetValue(key, out var stats))
        {
            stats = new ChessPlayerStats
            {
                Name = key,
            };
            _chessStats[key] = stats;
        }

        if (!string.IsNullOrWhiteSpace(player.CharacterName))
            stats.DisplayName = player.CharacterName.Trim();
        else if (string.IsNullOrWhiteSpace(stats.DisplayName))
            stats.DisplayName = key;

        switch (result)
        {
            case ChessResultKind.Win:
                stats.Wins++;
                break;
            case ChessResultKind.Loss:
                stats.Losses++;
                break;
            default:
                stats.Draws++;
                break;
        }

        stats.LastPlayedUtc = DateTimeOffset.UtcNow;
    }

    private void EnsureChessStatsLoadedLocked()
    {
        if (_chessStatsLoaded)
            return;

        _chessStatsLoaded = true;

        var path = ChessStatsFilePath();
        if (path == null || !File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var saved = JsonSerializer.Deserialize<ChessStatsFile>(json);
            if (saved?.Players == null)
                return;

            if (_leaderboardMessageId == 0)
                _leaderboardMessageId = saved.LeaderboardMessageId;

            foreach (var (key, record) in saved.Players)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var name = string.IsNullOrWhiteSpace(record.Name) ? key : record.Name.Trim();
                _chessStats[key] = new ChessPlayerStats
                {
                    Name = name,
                    DisplayName = string.IsNullOrWhiteSpace(record.DisplayName)
                        ? name
                        : record.DisplayName.Trim(),
                    Wins = Math.Max(0, record.Wins),
                    Losses = Math.Max(0, record.Losses),
                    Draws = Math.Max(0, record.Draws),
                    LastPlayedUtc = record.LastPlayedUtc,
                };
            }

            Sawmill.Info($"Loaded {_chessStats.Count} chess leaderboard entr(ies).");
        }
        catch (Exception e)
        {
            Sawmill.Error($"Failed to load the chess leaderboard: {e}");
        }
    }

    private void SaveChessStatsLocked()
    {
        var path = ChessStatsFilePath();
        if (path == null)
            return;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var file = new ChessStatsFile
            {
                LeaderboardMessageId = _leaderboardMessageId,
            };

            foreach (var (key, stats) in _chessStats)
            {
                file.Players[key] = new ChessPlayerStatsRecord
                {
                    Name = stats.Name,
                    DisplayName = stats.DisplayName,
                    Wins = stats.Wins,
                    Losses = stats.Losses,
                    Draws = stats.Draws,
                    LastPlayedUtc = stats.LastPlayedUtc,
                };
            }

            var json = JsonSerializer.Serialize(
                file,
                new JsonSerializerOptions { WriteIndented = true });

            // Atomic replace so a crash cannot leave the leaderboard half-written.
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception e)
        {
            Sawmill.Error($"Failed to save the chess leaderboard: {e}");
        }
    }

    private void PersistLeaderboardMessageId()
    {
        lock (_chessStatsLock)
        {
            EnsureChessStatsLoadedLocked();
            SaveChessStatsLocked();
        }
    }

    private string? ChessStatsFilePath()
    {
        var root = _resources.UserData.RootDir;
        return string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine(root, LeaderboardFileName);
    }

    /// <summary>
    /// Returns the top players by wins. Used by the public <c>/chess/leaderboard</c>
    /// status-host endpoint.
    /// </summary>
    public List<ChessLeaderboardEntry> GetChessLeaderboard(int count = 10)
    {
        count = Math.Clamp(count, 1, 100);

        lock (_chessStatsLock)
        {
            EnsureChessStatsLoadedLocked();
            return _chessStats.Values
                .OrderByDescending(stats => stats.Wins)
                .ThenBy(stats => stats.Losses)
                .ThenByDescending(stats => stats.Draws)
                .ThenBy(stats => stats.Name, StringComparer.OrdinalIgnoreCase)
                .Take(count)
                .Select(stats =>
                {
                    var games = stats.Wins + stats.Losses + stats.Draws;
                    var winRate = games == 0 ? 0d : Math.Round(stats.Wins * 100d / games, 1);
                    return new ChessLeaderboardEntry(
                        stats.Name,
                        string.IsNullOrWhiteSpace(stats.DisplayName) ? stats.Name : stats.DisplayName,
                        stats.Wins,
                        stats.Losses,
                        stats.Draws,
                        games,
                        winRate,
                        stats.LastPlayedUtc);
                })
                .ToList();
        }
    }

    /// <summary>Number of distinct SS14 players that have at least one recorded game.</summary>
    public int ChessLeaderboardPlayerCount
    {
        get
        {
            lock (_chessStatsLock)
            {
                EnsureChessStatsLoadedLocked();
                return _chessStats.Count;
            }
        }
    }

    /// <summary>
    /// Refreshes the Discord leaderboard at the end of every round. The first round creates
    /// the message; later rounds edit the same message instead of posting a new one.
    /// </summary>
    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        _ = UpdateLeaderboardWebhookAsync();
    }

    private void ResetLeaderboardWebhook()
    {
        _leaderboardWebhookUrl = string.Empty;
        _leaderboardWebhookIdentifier = default;
        _leaderboardMessageId = 0;
    }

    private async Task UpdateLeaderboardWebhookAsync()
    {
        if (string.IsNullOrWhiteSpace(_resultWebhookUrl))
            return;

        if (Interlocked.Exchange(ref _leaderboardWebhookBusy, 1) == 1)
            return;

        try
        {
            var url = _resultWebhookUrl;
            if (string.IsNullOrWhiteSpace(url))
                return;

            var webhookChanged = !string.IsNullOrEmpty(_leaderboardWebhookUrl) &&
                                 !string.Equals(_leaderboardWebhookUrl, url, StringComparison.Ordinal);

            if (_leaderboardWebhookIdentifier == default || webhookChanged)
            {
                if (await _discord.GetWebhook(url) is not { } webhook)
                    return;

                _leaderboardWebhookIdentifier = webhook.ToIdentifier();
                _leaderboardWebhookUrl = url;

                // A restarted server keeps the message id loaded from disk. Only a real
                // webhook change invalidates it.
                if (webhookChanged)
                    _leaderboardMessageId = 0;
            }

            var payload = BuildLeaderboardPayload();

            if (_leaderboardMessageId == 0)
            {
                using var response = await _discord.CreateMessage(_leaderboardWebhookIdentifier, payload);
                if (!response.IsSuccessStatusCode)
                {
                    Sawmill.Warning(
                        $"Chess: Discord rejected leaderboard message: {(int) response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.TryGetProperty("id", out var idElement) &&
                    ulong.TryParse(idElement.GetString(), out var messageId))
                {
                    _leaderboardMessageId = messageId;
                    PersistLeaderboardMessageId();
                    Sawmill.Info("Chess: created the Discord leaderboard message.");
                }
            }
            else
            {
                using var response = await _discord.EditMessage(
                    _leaderboardWebhookIdentifier,
                    _leaderboardMessageId,
                    payload);

                if ((int) response.StatusCode == 404)
                {
                    // The message was deleted in Discord; recreate it on the next round.
                    _leaderboardMessageId = 0;
                    PersistLeaderboardMessageId();
                    Sawmill.Warning("Chess: Discord leaderboard message no longer exists.");
                }
                else if (!response.IsSuccessStatusCode)
                {
                    Sawmill.Warning(
                        $"Chess: Discord rejected leaderboard update: {(int) response.StatusCode}");
                }
                else
                {
                    Sawmill.Info("Chess: updated the Discord leaderboard message.");
                }
            }
        }
        catch (Exception e)
        {
            Sawmill.Error($"Chess: failed to update the Discord leaderboard: {e}");
        }
        finally
        {
            Interlocked.Exchange(ref _leaderboardWebhookBusy, 0);
        }
    }

    private WebhookPayload BuildLeaderboardPayload()
    {
        var entries = GetChessLeaderboard(10);
        var lines = new List<string>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var name = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.Name
                : entry.DisplayName;

            lines.Add(
                $"**{i + 1}.** {name} — **{entry.Wins}** побед / {entry.Losses} поражений / {entry.Draws} ничьих");
        }

        if (lines.Count == 0)
            lines.Add("Пока нет сыгранных партий.");

        return new WebhookPayload
        {
            Username = "Chess.NT",
            Embeds =
            [
                new WebhookEmbed
                {
                    Title = "Chess.NT — таблица лидеров (топ 10 по победам)",
                    Description = string.Join("\n", lines),
                    Color = 0xC9A227,
                    Footer = new WebhookEmbedFooter
                    {
                        Text = $"Обновлено {DateTimeOffset.UtcNow:dd.MM.yyyy HH:mm} UTC · " +
                               $"всего игроков: {ChessLeaderboardPlayerCount}",
                    },
                    Fields = new List<WebhookEmbedField>(),
                },
            ],
        };
    }

    private enum ChessResultKind
    {
        Win,
        Loss,
        Draw,
    }

    private sealed class ChessPlayerStats
    {
        public string Name = string.Empty;
        public string DisplayName = string.Empty;
        public int Wins;
        public int Losses;
        public int Draws;
        public DateTimeOffset LastPlayedUtc;
    }

    private sealed class ChessStatsFile
    {
        public Dictionary<string, ChessPlayerStatsRecord> Players { get; set; } = new();

        /// <summary>
        /// Discord message that holds the leaderboard. Persisting it lets a restarted
        /// server edit the existing message instead of posting a duplicate.
        /// </summary>
        public ulong LeaderboardMessageId { get; set; }
    }

    private sealed class ChessPlayerStatsRecord
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public DateTimeOffset LastPlayedUtc { get; set; }
    }
}
