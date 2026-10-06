using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;

namespace Content.Server._Starlight.Statistics;

public sealed partial class RoundStatisticsSystem
{
    [Dependency] private IGameMapManager _gameMapManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private int _peakPlayerCount;
    private int _roundStartPlayerCount;
    private int _lateJoinCount;
    private int _playerDeaths;
    private string? _resolvedPreset;

    /// <summary>
    /// Records the preset a wrapper preset actually rolled, so a Secret round is not just "Secret".
    /// </summary>
    public void RecordResolvedPreset(string presetId)
    {
        if (!EnsureRound())
            return;

        _resolvedPreset = presetId;
    }

    private void InitializeRoundStatistics()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChangedForRound);
        RegisterStatisticsDomain(EmitRoundSummary, ClearRoundSummary);
    }

    /// <summary>
    /// The system's only spawn subscription; every statistics domain is dispatched from here.
    /// </summary>
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!EnsureRound())
            return;

        if (args.LateJoin)
            _lateJoinCount++;
        else
            _roundStartPlayerCount++;

        SamplePlayerCount();
        RecordJobSpawnComplete(args);
    }

    private void OnMobStateChangedForRound(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (!_mind.TryGetMind(args.Target, out _, out var mind) || mind.UserId == null)
            return;

        if (!EnsureRound())
            return;

        _playerDeaths++;
    }

    private void SamplePlayerCount()
        => _peakPlayerCount = Math.Max(_peakPlayerCount, _playerManager.PlayerCount);

    private void EmitRoundSummary(RoundEndMessageEvent args)
    {
        SamplePlayerCount();

        var preset = _gameTicker.CurrentPreset?.ID ?? "unknown";
        var map = _gameMapManager.GetSelectedMap()?.ID ?? "unknown";

        var antags = 0;
        var observers = 0;
        var connected = 0;
        foreach (var player in args.AllPlayersEndInfo)
        {
            if (player.Antag)
                antags++;

            if (player.Observer)
                observers++;

            if (player.Connected)
                connected++;
        }

        EmitRoundRecord(
            args.RoundId,
            "kind=round_summary preset_id={PresetId} resolved_preset_id={ResolvedPresetId} map_id={MapId} " +
            "duration_seconds={DurationSeconds} " +
            "players_roundstart={PlayersRoundStart} players_latejoin={PlayersLateJoin} players_peak={PlayersPeak} " +
            "players_end={PlayersEnd} players_connected={PlayersConnected} antags={Antags} observers={Observers}",
            preset,
            _resolvedPreset ?? preset,
            map,
            Number(args.RoundDuration.TotalSeconds),
            _roundStartPlayerCount,
            _lateJoinCount,
            _peakPlayerCount,
            args.PlayerCount,
            connected,
            antags,
            observers);

        EmitPopulationSummary(args.RoundId);
    }

    /// <summary>
    /// Emits the survival breakdown of every player-backed mind at round end. Bodyless minds are
    /// counted separately so they do not inflate the death count.
    /// </summary>
    private void EmitPopulationSummary(int roundId)
    {
        var alive = 0;
        var critical = 0;
        var dead = 0;
        var bodyless = 0;

        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out _, out var mind))
        {
            if (mind.UserId == null)
                continue;

            if (mind.OwnedEntity is not { } body || !TryComp<MobStateComponent>(body, out var state))
            {
                bodyless++;
                continue;
            }

            switch (state.CurrentState)
            {
                case MobState.Alive:
                    alive++;
                    break;
                case MobState.Critical:
                    critical++;
                    break;
                case MobState.Dead:
                    dead++;
                    break;
                default:
                    bodyless++;
                    break;
            }
        }

        EmitRoundRecord(
            roundId,
            "kind=population alive={Alive} critical={Critical} dead={Dead} bodyless={Bodyless} deaths={Deaths}",
            alive,
            critical,
            dead,
            bodyless,
            _playerDeaths);
    }

    private void ClearRoundSummary()
    {
        _peakPlayerCount = 0;
        _roundStartPlayerCount = 0;
        _lateJoinCount = 0;
        _playerDeaths = 0;
        _resolvedPreset = null;
    }
}
