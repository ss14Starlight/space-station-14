using System;
using System.Linq;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Shared.Audio.Events;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Prototypes;
using Content.Shared._Starlight.CCVar;
using Robust.Client;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Audio;

// Part of ContentAudioSystem that is responsible for lobby music playing/stopping and round-end sound-effect.
public sealed partial class ContentAudioSystem
{
    [Dependency] private IBaseClient _client = default!;
    [Dependency] private ClientGameTicker _gameTicker = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly AudioParams _lobbySoundtrackParams = new(-5f, 1, 0, 0, 0, false, 0f);
    private readonly AudioParams _roundEndSoundEffectParams = new(-5f, 1, 0, 0, 0, false, 0f);

    /// <summary>
    /// EntityUid of lobby restart sound component.
    /// </summary>
    private EntityUid? _lobbyRoundRestartAudioStream;

    /// <summary>
    /// Shuffled list of soundtrack file-names.
    /// </summary>
    private string[]? _lobbyPlaylist;

    /// <summary>
    /// Short info about lobby soundtrack currently playing. Is null if soundtrack is not playing.
    /// </summary>
    private LobbySoundtrackInfo? _lobbySoundtrackInfo;

    private Action<LobbySoundtrackChangedEvent>? _lobbySoundtrackChanged;

    /// <summary>
    /// Event for subscription on lobby soundtrack changes.
    /// </summary>
    public event Action<LobbySoundtrackChangedEvent>? LobbySoundtrackChanged
    {
        add
        {
            if (value != null)
            {
                if (_lobbySoundtrackInfo != null)
                {
                    value(new LobbySoundtrackChangedEvent(_lobbySoundtrackInfo.Filename));
                }

                _lobbySoundtrackChanged += value;
            }
        }
        remove => _lobbySoundtrackChanged -= value;
    }

    /// <summary>
    /// Initializes subscriptions that are related to lobby music.
    /// </summary>
    private void InitializeLobbyMusic()
    {
        Subs.CVar(_configManager, CCVars.LobbyMusicEnabled, LobbyMusicCVarChanged);
        Subs.CVar(_configManager, CCVars.LobbyMusicVolume, LobbyMusicVolumeCVarChanged);

        _state.OnStateChanged += StateManagerOnStateChanged;

        _client.PlayerLeaveServer += OnLeave;

        SubscribeNetworkEvent<LobbyMusicStopEvent>(OnLobbySongStopped);
        SubscribeNetworkEvent<LobbyPlaylistChangedEvent>(OnLobbySongChanged);
        SubscribeNetworkEvent<RoundEndCancelMessageEvent>(OnRoundEndCancelMessage); // Daiquiri
    }

    /// <summary>
    /// Fired when the lobby playlist changes (track membership, shuffle, etc).
    /// </summary>
    public event Action? LobbyPlaylistChanged;

    private void OnLobbySongStopped(LobbyMusicStopEvent ev)
    {
        EndLobbyMusic();
    }

    private void StateManagerOnStateChanged(StateChangedEventArgs args)
    {
        switch (args.NewState)
        {
            case LobbyState:
                var resume = CaptureLobbyResume();
                _postRoundMusic = false;
                StopAllAudio();
                StartLobbyMusic(resume);
                break;
            default:
                // Daiquiri
                if (!_postRoundMusic)
                    EndLobbyMusic();
                break;
        }
    }

    private void OnLeave(object? sender, PlayerEventArgs args)
    {
        _postRoundMusic = false; // Daiquiri
        EndLobbyMusic();
        _configManager.SaveToFile();
    }

    #region Starlight

    /// <summary>
    /// Whether lobby music is currently paused.
    /// </summary>
    public bool LobbyMusicPaused => _lobbyPauseStarted != null;
    private TimeSpan? _lobbyPauseStarted;
    private float _lobbyPausePosition;
    private TimeSpan _lobbyTrackStartedAt;

    /// <summary>
    /// How long a freshly started stream may take to actually begin playing before we
    /// consider it dead. Prevents a track from being skipped during a long frame/map load.
    /// </summary>
    private static readonly TimeSpan LobbyTrackStartGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Playback position is authoritative for "did this track finish?". Wall-clock
    /// timing can jump during long stalls/round restarts, which used to skip through
    /// the whole playlist in a single frame.
    /// </summary>
    private const float LobbyTrackEndTolerance = 0.75f;

    /// <summary>
    /// Current track length in seconds, if any.
    /// </summary>
    public float? LobbyTrackLengthSeconds { get; private set; }

    /// <summary>
    /// Current playback position in seconds, if any. Never throws (dead audio device safe).
    /// </summary>
    public float? LobbyTrackPositionSeconds
    {
        get
        {
            if (_lobbySoundtrackInfo == null)
                return null;
            try
            {
                if (!TryComp(_lobbySoundtrackInfo.MusicStreamEntityUid, out AudioComponent? comp))
                    return null;
                return comp.PlaybackPosition;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Seek the current lobby track, keeping autoplay timing intact.
    /// </summary>
    public void SeekLobbyTrack(float seconds)
    {
        if (_lobbySoundtrackInfo == null || LobbyTrackLengthSeconds is not { } length)
            return;

        var clamped = Math.Clamp(seconds, 0f, length);
        if (TryComp(_lobbySoundtrackInfo.MusicStreamEntityUid, out AudioComponent? comp))
            comp.PlaybackPosition = clamped;
        if (LobbyMusicPaused)
        {
            // Daiquiri
            _lobbyPausePosition = clamped;
            _lobbyPauseStarted = _timing.RealTime;
        }
        _lobbySoundtrackInfo = _lobbySoundtrackInfo with
        {
            NextTrackOn = _timing.RealTime + TimeSpan.FromSeconds(Math.Max(0.0, length - clamped))
        };
    }

    /// <summary>
    /// Pause or resume the current lobby track, keeping autoplay timing intact.
    /// </summary>
    public void SetLobbyMusicPaused(bool paused)
    {
        if (_lobbySoundtrackInfo == null)
            return;
        if (paused == LobbyMusicPaused)
            return;

        if (paused)
        {
            try
            {
                if (TryComp(_lobbySoundtrackInfo.MusicStreamEntityUid, out AudioComponent? pauseComp))
                {
                    _lobbyPausePosition = pauseComp.PlaybackPosition;
                    pauseComp.Pause();
                }
            }
            catch
            {
                // Dead audio device: keep pause state, stream gets rebuilt on resume.
            }
            _lobbyPauseStarted = _timing.RealTime;
        }
        else
        {
            // Daiquiri
            // entity: a long-paused source can be evicted from memory (OS pressure,
            // device sleep, driver cleanup) and StartPlaying won't revive it.
            _lobbyPauseStarted = null;
            var file = _lobbySoundtrackInfo.Filename;
            var pos = _lobbyPausePosition;
            try
            {
                EndLobbyMusicSilent();
                PlaySoundtrack(file);
                if (_lobbySoundtrackInfo != null)
                {
                    SeekLobbyTrack(pos);
                    return;
                }
                _sawmill.Warning("Lobby resume failed: stream did not start.");
            }
            catch (Exception e)
            {
                _sawmill.Error($"Lobby resume failed: {e}");
                _lobbySoundtrackInfo = null;
                LobbyTrackLengthSeconds = null;
            }
            // Resume failed: tell the UI the music is stopped so the button state
            // resets and the user can restart via skip/track select.
            _lobbySoundtrackChanged?.Invoke(new LobbySoundtrackChangedEvent());
        }
    }

    /// <summary>
    /// Stops the current stream without firing change events or clearing pause position.
    /// </summary>
    private void EndLobbyMusicSilent()
    {
        if (_lobbySoundtrackInfo == null)
            return;
        _audio.Stop(_lobbySoundtrackInfo.MusicStreamEntityUid);
        _lobbySoundtrackInfo = null;
        LobbyTrackLengthSeconds = null;
    }

    /// <summary>
    /// Whether there is a lobby playlist available to skip through.
    /// </summary>
    public bool HasLobbyPlaylist => _lobbyPlaylist is { Length: > 0 };

    /// <summary>
    /// Current lobby playlist filenames, LOCAL view: server order minus tracks
    /// this player excluded. Never leaves this client.
    /// </summary>
    public IReadOnlyList<string>? LobbyPlaylistTracks => LocalPool() is { Length: > 0 } pool ? pool : null;

    /// <summary>
    /// Currently playing lobby track filename, if any.
    /// </summary>
    public string? CurrentLobbyTrack => _lobbySoundtrackInfo?.Filename;

    /// <summary>
    /// Daiquiri
    /// </summary>
    private readonly Dictionary<string, HashSet<string>> _localRemovedTracks = new();

    /// <summary>
    /// Daiquiri
    /// </summary>
    private readonly HashSet<string> _localRemovedGlobal = new();

    private bool _queueStateLoaded;
    private readonly HashSet<string> _touchedPlaylists = new();

    /// <summary>
    /// Daiquiri
    /// playlist). Appended to the local pool; never sent to server.
    /// </summary>
    private readonly List<string> _localAddedTracks = new();

    /// <summary>
    /// Daiquiri
    /// </summary>
    private List<string>? _localOrder;

    /// <summary>
    /// Daiquiri
    /// the player never touched. Runs once, lazily.
    /// </summary>
    private void EnsureQueueState()
    {
        if (_queueStateLoaded)
            return;
        _queueStateLoaded = true;
        LoadQueueState();
        foreach (var playlist in _proto.EnumeratePrototypes<LobbyPlaylistPrototype>())
        {
            if (playlist.TurnOn)
                continue;
            if (_touchedPlaylists.Contains(playlist.ID))
                continue;
            if (_localRemovedTracks.ContainsKey(playlist.ID))
                continue;
            _localRemovedTracks[playlist.ID] =
                new HashSet<string>(playlist.Tracks.Select(t => t.ToString()));
        }
    }

    private void LoadQueueState()
    {
        // Daiquiri
        foreach (var entry in _configManager.GetCVar(StarlightCCVars.LobbyQueueRemoved)
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('|', 2);
            if (parts.Length != 2)
                continue;
            if (parts[0] == "*")
            {
                _localRemovedGlobal.Add(parts[1]);
                continue;
            }
            if (!_localRemovedTracks.TryGetValue(parts[0], out var set))
            {
                set = new HashSet<string>();
                _localRemovedTracks[parts[0]] = set;
            }
            set.Add(parts[1]);
        }
        foreach (var playlist in _configManager.GetCVar(StarlightCCVars.LobbyQueueTouched)
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            _touchedPlaylists.Add(playlist);
        }
        foreach (var track in _configManager.GetCVar(StarlightCCVars.LobbyQueueAdded)
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!_localAddedTracks.Contains(track))
                _localAddedTracks.Add(track);
        }
        var savedOrder = _configManager.GetCVar(StarlightCCVars.LobbyQueueOrder)
            .Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (savedOrder.Count > 0)
            _localOrder = savedOrder;
    }

    private void SaveQueueState()
    {
        var removed = string.Join(';', _localRemovedTracks
            .SelectMany(kv => kv.Value.Select(t => $"{kv.Key}|{t}"))
            .Concat(_localRemovedGlobal.Select(t => $"*|{t}")));
        _configManager.SetCVar(StarlightCCVars.LobbyQueueRemoved, removed);
        _configManager.SetCVar(StarlightCCVars.LobbyQueueTouched, string.Join(';', _touchedPlaylists));
        _configManager.SetCVar(StarlightCCVars.LobbyQueueAdded, string.Join(';', _localAddedTracks));
        _configManager.SetCVar(StarlightCCVars.LobbyQueueOrder, string.Join(';', _localOrder ?? new List<string>()));
        // Daiquiri
        _configManager.SaveToFile();
    }

    /// <summary>
    /// Add or remove a track from a lobby playlist. Local only: affects just this player's pool.
    /// </summary>
    public void SetTrackMembership(string playlistId, string trackPath, bool add)
    {
        EnsureQueueState();
        if (add)
        {
            // Daiquiri
            // in several playlists whose untouched turnon:false sets would veto it.
            foreach (var set in _localRemovedTracks.Values)
                set.Remove(trackPath);
            _localRemovedGlobal.Remove(trackPath);
            if ((_lobbyPlaylist == null || !_lobbyPlaylist.Contains(trackPath))
                && !_localAddedTracks.Contains(trackPath))
                _localAddedTracks.Add(trackPath);
        }
        else
        {
            if (!_localRemovedTracks.TryGetValue(playlistId, out var set))
            {
                set = new HashSet<string>();
                _localRemovedTracks[playlistId] = set;
            }
            set.Add(trackPath);
            _localAddedTracks.Remove(trackPath);
        }
        _touchedPlaylists.Add(playlistId);
        ReconcileOrder();
        PauseIfPoolEmpty();
        LobbyPlaylistChanged?.Invoke();
    }

    /// <summary>
    /// Daiquiri
    /// </summary>
    public void ClearQueue()
    {
        EnsureQueueState();
        var all = LocalPool().ToList();
        _localAddedTracks.Clear();
        _localOrder = null;
        foreach (var track in all)
        {
            var containers = _proto.EnumeratePrototypes<LobbyPlaylistPrototype>()
                .Where(p => p.Tracks.Any(t => t.ToString() == track))
                .Select(p => p.ID)
                .ToList();
            if (containers.Count == 0)
            {
                _localRemovedGlobal.Add(track);
                continue;
            }
            foreach (var id in containers)
            {
                if (!_localRemovedTracks.TryGetValue(id, out var set))
                {
                    set = new HashSet<string>();
                    _localRemovedTracks[id] = set;
                }
                set.Add(track);
                _touchedPlaylists.Add(id);
            }
        }
        SaveQueueState();
        PauseIfPoolEmpty();
        LobbyPlaylistChanged?.Invoke();
    }

    /// <summary>
    /// Daiquiri
    /// Keeps the saved order stable, drops gone tracks, appends fresh ones.
    /// </summary>
    private void ReconcileOrder()
    {
        EnsureQueueState();
        if (_lobbyPlaylist is not { Length: > 0 })
        {
            SaveQueueState();
            return;
        }
        var baseTracks = BasePoolTracks();
        var server = new HashSet<string>(baseTracks);
        var order = _localOrder ?? new List<string>();
        var kept = order.Where(t => server.Contains(t) && !IsLocallyExcluded(t)).ToList();
        kept.AddRange(baseTracks.Where(t => !kept.Contains(t) && !IsLocallyExcluded(t)));
        _localOrder = kept;
        SaveQueueState();
    }

    /// <summary>
    /// Include or exclude ALL tracks of a playlist. Local only.
    /// </summary>
    public void SetPlaylistAll(string playlistId, bool include)
    {
        EnsureQueueState();
        if (include)
        {
            _localRemovedTracks.Remove(playlistId);
            // Daiquiri
            // so selecting them all must re-add them explicitly (same as a single checkbox).
            if (_proto.TryIndex<LobbyPlaylistPrototype>(playlistId, out var proto))
            {
                foreach (var t in proto.Tracks.Select(t => t.ToString()))
                {
                    if ((_lobbyPlaylist == null || !_lobbyPlaylist.Contains(t))
                        && !_localAddedTracks.Contains(t))
                        _localAddedTracks.Add(t);
                }
            }
        }
        else
        {
            if (!_proto.TryIndex<LobbyPlaylistPrototype>(playlistId, out var proto))
                return;
            var tracks = proto.Tracks.Select(t => t.ToString()).ToList();
            _localRemovedTracks[playlistId] = new HashSet<string>(tracks);
            foreach (var t in tracks)
                _localAddedTracks.Remove(t);
        }
        _touchedPlaylists.Add(playlistId);
        ReconcileOrder();
        PauseIfPoolEmpty();
        LobbyPlaylistChanged?.Invoke();
    }

    /// <summary>
    /// Whether a track counts as included in a playlist for this player.
    /// </summary>
    public bool IsTrackIncluded(string playlistId, string trackPath)
    {
        EnsureQueueState();
        if (_localRemovedTracks.TryGetValue(playlistId, out var removed) && removed.Contains(trackPath))
            return false;
        return _proto.TryIndex<LobbyPlaylistPrototype>(playlistId, out var proto)
            && proto.Tracks.Any(t => t.ToString() == trackPath);
    }

    /// <summary>
    /// Daiquiri
    /// </summary>
    private void PauseIfPoolEmpty()
    {
        if (LocalPool() is { Length: > 0 })
            return;
        if (_lobbySoundtrackInfo != null && !LobbyMusicPaused)
            SetLobbyMusicPaused(true);
    }

    /// <summary>
    /// Server pool plus every track whose checkbox is on in at least one playlist.
    /// </summary>
    private List<string> BasePoolTracks()
    {
        var tracks = new List<string>();
        if (_lobbyPlaylist is not { Length: > 0 })
            return tracks;

        tracks.AddRange(_lobbyPlaylist);
        foreach (var playlist in _proto.EnumeratePrototypes<LobbyPlaylistPrototype>())
        {
            foreach (var track in playlist.Tracks)
            {
                var path = track.ToString();
                if (tracks.Contains(path) || !IsTrackIncluded(playlist.ID, path))
                    continue;

                tracks.Add(path);
            }
        }
        return tracks;
    }

    /// <summary>
    /// Server pool minus locally excluded tracks, in the persisted local order.
    /// A track plays while at least one playlist containing it still includes it.
    /// </summary>
    private string[] LocalPool()
    {
        EnsureQueueState();
        if (_lobbyPlaylist is not { Length: > 0 } && _localAddedTracks.Count == 0)
            return [];
        var server = BasePoolTracks();
        List<string> ordered;
        if (_localOrder != null)
        {
            ordered = _localOrder
                .Where(t => server.Contains(t) && !IsLocallyExcluded(t))
                .ToList();
            if (ordered.Count == 0)
            {
                var live = server.Where(t => !IsLocallyExcluded(t)).ToList();
                if (live.Count > 0)
                {
                    _localOrder = null;
                    ordered = live;
                }
            }
        }
        else
        {
            ordered = server.Where(t => !IsLocallyExcluded(t)).ToList();
        }
        // Daiquiri
        foreach (var t in server)
        {
            if (!ordered.Contains(t) && !IsLocallyExcluded(t))
                ordered.Add(t);
        }
        foreach (var t in _localAddedTracks)
        {
            if (!ordered.Contains(t) && !IsLocallyExcluded(t))
                ordered.Add(t);
        }
        return ordered.ToArray();
    }

    private bool IsLocallyExcluded(string track)
    {
        // Daiquiri
        if (_localRemovedGlobal.Contains(track))
            return true;
        var containers = _proto.EnumeratePrototypes<LobbyPlaylistPrototype>()
            .Where(p => p.Tracks.Any(t => t.ToString() == track))
            .Select(p => p.ID)
            .ToList();
        if (containers.Count == 0)
            return false;
        return containers.All(id =>
            _localRemovedTracks.TryGetValue(id, out var removed) && removed.Contains(track));
    }

    /// <summary>
    /// Manually switch to a specific lobby track by filename.
    /// </summary>
    public void PlayLobbyTrack(string filename)
    {
        var pool = LocalPool();
        if (pool is not { Length: > 0 }
            || Array.IndexOf(pool, filename) < 0
            || !_configManager.GetCVar(CCVars.LobbyMusicEnabled)
            || _state.CurrentState is not LobbyState)
        {
            return;
        }

        EndLobbyMusic();
        PlaySoundtrack(filename);
    }

    /// <summary>
    /// Play a lobby track even if it is not in the current server pool
    /// (used when a click auto-adds the track: the pool updates async).
    /// </summary>
    public void PlayLobbyTrackForced(string filename)
    {
        if (!_configManager.GetCVar(CCVars.LobbyMusicEnabled)
            || _state.CurrentState is not LobbyState)
        {
            return;
        }

        EndLobbyMusic();
        PlaySoundtrack(filename);
    }

    /// <summary>
    /// Manually switch to the next lobby track. Wraps around at the end.
    /// </summary>
    public void PlayNextLobbyTrack()
    {
        SkipLobbyTrack(1);
    }

    /// <summary>
    /// Manually switch to the previous lobby track. Wraps around at the start.
    /// </summary>
    public void PlayPreviousLobbyTrack()
    {
        SkipLobbyTrack(-1);
    }

    private void SkipLobbyTrack(int direction)
    {
        var pool = ActivePool() is { Length: > 0 } active ? active : LocalPool();
        if (pool is not { Length: > 0 }
            || !_configManager.GetCVar(CCVars.LobbyMusicEnabled)
            || _state.CurrentState is not LobbyState)
        {
            return;
        }

        var current = _lobbySoundtrackInfo?.Filename;
        var index = current == null ? -1 : Array.IndexOf(pool, current);
        var nextIndex = (index + direction + pool.Length) % pool.Length;

        PlayLobbyTrack(pool[nextIndex]);
    }

    /// <summary>
    /// Daiquiri
    /// </summary>
    private string? _activePlaylistId;

    public void SetActivePlaylist(string? playlistId)
    {
        _activePlaylistId = playlistId;
    }

    private string[]? ActivePool()
    {
        if (_activePlaylistId == null)
            return null;
        if (!_proto.TryIndex<LobbyPlaylistPrototype>(_activePlaylistId, out var proto))
            return null;
        var included = proto.Tracks
            .Select(t => t.ToString())
            .Where(t => IsTrackIncluded(_activePlaylistId, t))
            .ToArray();
        return included.Length > 0 ? included : null;
    }

    #endregion

    private void LobbyMusicVolumeCVarChanged(float volume)
    {
        if (_lobbySoundtrackInfo != null)
        {
            _audio.SetVolume(
                _lobbySoundtrackInfo.MusicStreamEntityUid,
                _lobbySoundtrackParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume))
            );
        }
    }

    private void LobbyMusicCVarChanged(bool musicEnabled)
    {
        if (musicEnabled && _state.CurrentState is LobbyState)
        {
            StartLobbyMusic();
        }
        else
        {
            EndLobbyMusic();
        }
    }

    private void OnLobbySongChanged(LobbyPlaylistChangedEvent playlistChangedEvent)
    {
        var playlist = playlistChangedEvent.Playlist;
        _postRoundMusic = playlistChangedEvent.PostRound; // Daiquiri
        var current = _lobbySoundtrackInfo?.Filename;

        if (current != null
            && (playlist.Contains(current) || _localAddedTracks.Contains(current)))
        {
            _lobbyPlaylist = playlist;
            ReconcileOrder();
            LobbyPlaylistChanged?.Invoke();
            return;
        }

        _lobbyPlaylist = playlist;
        // Daiquiri
        ReconcileOrder();
        EndLobbyMusic();
        StartLobbyMusic(playlist);
        LobbyPlaylistChanged?.Invoke();
    }

    private (string Track, float Position)? CaptureLobbyResume()
    {
        if (_lobbySoundtrackInfo == null || LobbyMusicPaused)
            return null;

        try
        {
            if (!TryComp(_lobbySoundtrackInfo.MusicStreamEntityUid, out AudioComponent? comp)
                || !comp.Playing)
            {
                return null;
            }

            return (_lobbySoundtrackInfo.Filename, comp.PlaybackPosition);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns true if the current lobby soundtrack stream still exists and is playing.
    /// A paused soundtrack is considered alive, since it can be resumed.
    /// </summary>
    private bool IsLobbySoundtrackAlive()
    {
        if (_lobbySoundtrackInfo == null)
            return false;

        if (LobbyMusicPaused)
            return true;

        return TryComp(_lobbySoundtrackInfo.MusicStreamEntityUid, out AudioComponent? comp) && comp.Playing;
    }

    /// <summary>
    /// Returns the track to start with: the last one this player heard if it is still
    /// available, otherwise the first track in the pool.
    /// </summary>
    private string GetResumeTrack(string[] pool)
    {
        var last = _configManager.GetCVar(StarlightCCVars.LobbyLastTrack);
        if (!string.IsNullOrEmpty(last) && Array.IndexOf(pool, last) >= 0)
            return last;

        return pool[0];
    }

    /// <summary>
    /// Re-starts playing lobby music from playlist, last sent from server. if there is currently none - does nothing.
    /// </summary>
    private void StartLobbyMusic((string Track, float Position)? resume = null)
    {
        if (!_configManager.GetCVar(CCVars.LobbyMusicEnabled)
            || _state.CurrentState is not LobbyState)
        {
            return;
        }

        if (_lobbySoundtrackInfo != null)
        {
            if (IsLobbySoundtrackAlive())
                return;

            // The stream was stopped or cleaned up (e.g. by a state transition) while the
            // soundtrack info survived. Drop the stale info so a new stream can be started.
            EndLobbyMusic();
        }

        // Daiquiri
        var pool = LocalPool();
        if (pool.Length == 0)
        {
            return;
        }

        var track = GetResumeTrack(pool);
        PlaySoundtrack(track);

        if (resume is { } previous && previous.Track == track)
            SeekLobbyTrack(previous.Position);
    }

    /// <summary>
    /// Starts playing lobby music from playlist. If playlist is empty, or lobby music setting is turned off - does nothing.
    /// </summary>
    /// <param name="playlist">Array of soundtrack filenames for lobby playlist.</param>
    private void StartLobbyMusic(string[] playlist)
    {
        if (!_configManager.GetCVar(CCVars.LobbyMusicEnabled))
            return;

        // Post-round music is deliberately played while the client is still in
        // GameplayState (the round-end scoreboard). Only allow the playlist to start
        // outside the lobby in that case.
        if (_state.CurrentState is not LobbyState && !_postRoundMusic)
            return;

        _lobbyPlaylist = playlist;
        if (_lobbyPlaylist.Length == 0)
            return;

        if (_lobbySoundtrackInfo != null)
        {
            if (IsLobbySoundtrackAlive())
                return;

            EndLobbyMusic();
        }

        var pool = LocalPool();
        if (pool.Length == 0)
            return;

        PlaySoundtrack(GetResumeTrack(pool));
    }

    private void PlaySoundtrack(string soundtrackFilename)
    {
        if (!_resourceCache.TryGetResource(new ResPath(soundtrackFilename), out AudioResource? audio))
        {
            _sawmill.Warning($"Lobby soundtrack resource not found: {soundtrackFilename}");
            return;
        }

        var volume = _lobbySoundtrackParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume));
        var playResult = _audio.PlayGlobal(
            soundtrackFilename,
            Filter.Local(),
            false,
            _lobbySoundtrackParams.WithVolume(volume)
        );
        if (playResult == null)
        {
            _sawmill.Warning(
                $"Tried to play lobby soundtrack '{{Filename}}' using {nameof(SharedAudioSystem)}.{nameof(SharedAudioSystem.PlayGlobal)} but it returned default value of EntityUid!",
                soundtrackFilename);
            return;
        }
        var nextTrackOn = _timing.RealTime + audio.AudioStream.Length;
        _lobbySoundtrackInfo = new LobbySoundtrackInfo(soundtrackFilename, nextTrackOn, playResult.Value.Entity);
        _lobbyTrackStartedAt = _timing.RealTime;
        _lobbyPauseStarted = null;
        LobbyTrackLengthSeconds = (float) audio.AudioStream.Length.TotalSeconds;
        _configManager.SetCVar(StarlightCCVars.LobbyLastTrack, soundtrackFilename);

        var lobbySongChangedEvent = new LobbySoundtrackChangedEvent(soundtrackFilename);
        _lobbySoundtrackChanged?.Invoke(lobbySongChangedEvent);
    }

    private void EndLobbyMusic()
    {
        if (_lobbySoundtrackInfo == null)
        {
            return;
        }

        _audio.Stop(_lobbySoundtrackInfo.MusicStreamEntityUid);
        _lobbySoundtrackInfo = null;
        _lobbyPauseStarted = null;
        LobbyTrackLengthSeconds = null;
        var lobbySongChangedEvent = new LobbySoundtrackChangedEvent();
        _lobbySoundtrackChanged?.Invoke(lobbySongChangedEvent);
    }

    private void PlayRestartSound(RoundRestartCleanupEvent ev)
    {
        if (!_configManager.GetCVar(CCVars.RestartSoundsEnabled))
            return;

        var file = _gameTicker.RestartSound;
        if (ResolvedSoundSpecifier.IsNullOrEmpty(file))
        {
            return;
        }

        _lobbyRoundRestartAudioStream = _audio.PlayGlobal(
            file,
            Filter.Local(),
            false,
            _roundEndSoundEffectParams.WithVolume(_roundEndSoundEffectParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume)))
        )?.Entity;
    }

    private void ShutdownLobbyMusic()
    {
        _state.OnStateChanged -= StateManagerOnStateChanged;

        _client.PlayerLeaveServer -= OnLeave;

        EndLobbyMusic();
        _configManager.SaveToFile();
    }

    private void UpdateLobbyMusic()
    {
        if (_lobbySoundtrackInfo == null)
        {
            return;
        }

        // Daiquiri
        if (LobbyMusicPaused)
        {
            return;
        }

        // Daiquiri
        var pool = ActivePool() is { Length: > 0 } active ? active : LocalPool();
        if (pool is not { Length: > 0 })
        {
            // Daiquiri
            // back; live ones (e.g. forced click-play) keep playing.
            var done = _timing.RealTime >= _lobbySoundtrackInfo.NextTrackOn;
            if (!done
                && (TryComp(_lobbySoundtrackInfo.MusicStreamEntityUid, out AudioComponent? aliveComp) && aliveComp.Playing))
            {
                return;
            }
            EndLobbyMusic();
            return;
        }

        var hasComp = TryComp(_lobbySoundtrackInfo.MusicStreamEntityUid, out AudioComponent? comp);
        var length = LobbyTrackLengthSeconds;
        var position = hasComp ? comp!.PlaybackPosition : 0f;
        var finished = hasComp
                       && length is { } trackLength
                       && position >= MathF.Max(0f, trackLength - LobbyTrackEndTolerance);

        // Daiquiri
        // Do not use _timing.RealTime for advancing. A long frame (map load, round restart,
        // debugger break) makes the wall clock jump past NextTrackOn and used to skip the
        // entire playlist. The stream's own playback position only moves when audio actually
        // played.
        if (hasComp && comp!.Playing)
        {
            if (!finished)
                return;
        }
        else
        {
            // Give a freshly created stream a moment to start before treating it as dead.
            if (_timing.RealTime - _lobbyTrackStartedAt < LobbyTrackStartGrace)
                return;

            if (!finished)
            {
                _sawmill.Debug("Lobby stream died mid-track, pausing instead of advancing.");
                if (length is { } deadLength)
                    _lobbyPausePosition = Math.Clamp(position, 0f, deadLength);
                SetLobbyMusicPaused(true);
                return;
            }
        }

        var nextSoundtrackFilename = GetNextSoundtrackFromPlaylist(_lobbySoundtrackInfo.Filename, pool);
        EndLobbyMusic();
        PlaySoundtrack(nextSoundtrackFilename);
    }

    private static string GetNextSoundtrackFromPlaylist(string currentSoundtrackFilename, string[] playlist)
    {
        var indexOfCurrent = Array.IndexOf(playlist, currentSoundtrackFilename);
        var nextTrackIndex = indexOfCurrent + 1;
        if (nextTrackIndex > playlist.Length - 1)
        {
            nextTrackIndex = 0;
        }

        return playlist[nextTrackIndex];
    }

    /// <summary> Container for lobby soundtrack information. </summary>
    /// <param name="Filename">Soundtrack filename.</param>
    /// <param name="NextTrackOn">Time (based on <see cref="IGameTiming.RealTime"/>) when this track is going to finish playing and next track have to be started.</param>
    /// <param name="MusicStreamEntityUid">
    /// EntityUid of launched soundtrack (from <see cref="SharedAudioSystem.PlayGlobal(string,Robust.Shared.Player.Filter,bool,System.Nullable{Robust.Shared.Audio.AudioParams})"/>).
    /// </param>
    private sealed record LobbySoundtrackInfo(string Filename, TimeSpan NextTrackOn, EntityUid MusicStreamEntityUid);
}

/// <summary>
/// Event of changing lobby soundtrack (or stopping lobby music - will pass null for <paramref name="SoundtrackFilename"/> in that case).
/// Is used by <see cref="ContentAudioSystem.LobbySoundtrackChanged"/> and <see cref="LobbyState.UpdateLobbySoundtrackInfo"/>.
/// </summary>
/// <param name="SoundtrackFilename">Filename of newly set soundtrack, or null if soundtrack playback is stopped.</param>
public sealed record LobbySoundtrackChangedEvent(string? SoundtrackFilename = null);
