using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Client.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Content.Client.CartridgeLoader.Cartridges;

public sealed class MusicPlayerSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resMan = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    private static readonly ISawmill Log = Logger.GetSawmill("music");

    private EntityUid? _currentAudioEntity;
    private string _currentTrackName = "";
    private float _volume = 0.5f;
    private float _currentTrackDuration = 0f;
    private readonly Dictionary<string, float> _durationCache = new();
    private readonly List<MusicPlayerUi> _activeUIs = new();
    private float _uiUpdateAccumulator = 0f;
    private const float UIUpdateInterval = 1.0f;
    private float _pausedPosition = 0f;
    private string _currentFilePath = "";
    private List<string> _playlistPaths = new();
    private List<string> _playlistNames = new();
    private int _currentTrackIndex = -1;
    private SharedAudioSystem? AudioSystem => EntityManager.System<SharedAudioSystem>();
    private bool _autoplayEnabled = true;
    private bool _shuffleEnabled = false;
    public bool ShuffleEnabled => _shuffleEnabled;

    public bool IsPlaying => _currentAudioEntity != null && AudioSystem?.IsPlaying(_currentAudioEntity) == true;

    public float CurrentPlaybackPosition
    {
        get
        {
            if (_currentAudioEntity != null && TryComp<AudioComponent>(_currentAudioEntity.Value, out var audioComp))
                return audioComp.PlaybackPosition;
            return _pausedPosition;
        }
    }

    public float GetTrackDuration() => _currentTrackDuration;
    public string CurrentTrackName => _currentTrackName;
    public float Volume => _volume;
    public float PausedPosition => _pausedPosition;

    public void RegisterUI(MusicPlayerUi ui)
    {
        _activeUIs.RemoveAll(u => u == null || u == ui);
        _activeUIs.Add(ui);
        ui.UpdateState(new MusicPlayerUiState(
            currentTrack: _currentTrackName,
            position: CurrentPlaybackPosition,
            duration: GetTrackDuration(),
            isPlaying: IsPlaying,
            volume: _volume,
            shuffleEnabled: _shuffleEnabled
        ));
    }

    public void UnregisterUI(MusicPlayerUi ui) => _activeUIs.Remove(ui);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiUpdateAccumulator += frameTime;
        if (_uiUpdateAccumulator >= UIUpdateInterval)
        {
            _uiUpdateAccumulator = 0f;
            foreach (var ui in _activeUIs)
                ui.UpdateUI();

            if (_autoplayEnabled && IsPlaying && _currentTrackDuration > 0)
            {
                if (CurrentPlaybackPosition >= _currentTrackDuration - 0.1f)
                    PlayNextTrack();
            }
        }
    }

    public void PlayTrack(string filePath, string trackName = "")
    {
        var audioSystem = AudioSystem;
        if (audioSystem == null)
        {
            Log.Error("SharedAudioSystem not available");
            return;
        }

        StopTrack();
        _currentFilePath = filePath;
        int playlistIndex = _playlistPaths.IndexOf(filePath);
        _currentTrackIndex = playlistIndex >= 0 ? playlistIndex : 0;
        _currentTrackDuration = GetAudioFileDuration(filePath);

        try
        {
            var volumeInDb = SharedAudioSystem.GainToVolume(_volume);
            var audioParams = AudioParams.Default.WithVolume(volumeInDb);
            var playResult = audioSystem.PlayGlobal(filePath, Filter.Local(), false, audioParams);

            if (playResult != null)
            {
                _currentAudioEntity = playResult.Value.Entity;
                audioSystem.SetVolume(_currentAudioEntity, volumeInDb);

                _currentTrackName = string.IsNullOrEmpty(trackName)
                    ? new ResPath(filePath).Filename.Split('.')[0]
                    : trackName;

                Log.Info($"Playing track: {_currentTrackName}, Volume: {_volume}, Duration: {_currentTrackDuration:F1}s)");
            }
            else
            {
                Log.Error($"Failed to play audio file: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error playing track {filePath}: {ex.Message}");
        }
    }

    public void StopTrack()
    {
        var audioSystem = AudioSystem;
        if (_currentAudioEntity != null && audioSystem != null)
        {
            audioSystem.Stop(_currentAudioEntity);
            _currentAudioEntity = null;
        }
        _currentTrackName = "";
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        var audioSystem = AudioSystem;
        if (_currentAudioEntity != null && audioSystem != null &&
            TryComp<AudioComponent>(_currentAudioEntity.Value, out var audioComp))
        {
            var volumeInDb = SharedAudioSystem.GainToVolume(_volume);
            audioSystem.SetVolume(_currentAudioEntity, volumeInDb);
        }
    }

    public void SetPosition(float position)
    {
        var audioSystem = AudioSystem;
        if (_currentAudioEntity != null && audioSystem != null)
            audioSystem.SetPlaybackPosition(_currentAudioEntity, position);
    }

    public void PauseTrack()
    {
        if (_currentAudioEntity != null)
        {
            if (TryComp<AudioComponent>(_currentAudioEntity.Value, out var audioComp))
                _pausedPosition = audioComp.PlaybackPosition;

            var audioSystem = AudioSystem;
            if (audioSystem != null)
            {
                audioSystem.Stop(_currentAudioEntity);
                _currentAudioEntity = null;
            }
        }
    }

    public void ResumeTrack()
    {
        if (!string.IsNullOrEmpty(_currentFilePath) && _pausedPosition > 0)
        {
            PlayTrack(_currentFilePath, _currentTrackName);
            var audioSystem = AudioSystem;
            if (_currentAudioEntity != null && audioSystem != null)
                audioSystem.SetPlaybackPosition(_currentAudioEntity, _pausedPosition);
            _pausedPosition = 0f;
        }
    }

    public void SetPlaylist(List<string> trackPaths, List<string> trackNames)
    {
        _playlistPaths = trackPaths;
        _playlistNames = trackNames;
    }

    public void PlayNextTrack()
    {
        if (_playlistPaths.Count == 0)
            return;

        if (_shuffleEnabled)
        {
            var rng = new Random();
            int nextIndex = rng.Next(_playlistPaths.Count);
            if (_playlistPaths.Count > 1 && nextIndex == _currentTrackIndex)
                nextIndex = (nextIndex + 1) % _playlistPaths.Count;
            _currentTrackIndex = nextIndex;
        }
        else
        {
            _currentTrackIndex++;
            if (_currentTrackIndex >= _playlistPaths.Count)
            {
                StopTrack();
                return;
            }
        }

        var nextPath = _playlistPaths[_currentTrackIndex];
        var nextName = _playlistNames.Count > _currentTrackIndex ? _playlistNames[_currentTrackIndex] : "";
        PlayTrack(nextPath, nextName);
    }

    public void ShufflePlaylist()
    {
        var rng = new Random();
        var zipped = _playlistPaths.Zip(_playlistNames, (path, name) => (path, name)).ToList();
        int n = zipped.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (zipped[n], zipped[k]) = (zipped[k], zipped[n]);
        }
        _playlistPaths = zipped.Select(z => z.path).ToList();
        _playlistNames = zipped.Select(z => z.name).ToList();
        _currentTrackIndex = 0;
        PlayTrack(_playlistPaths[0], _playlistNames[0]);
    }

    public void ToggleShuffle()
    {
        _shuffleEnabled = !_shuffleEnabled;
    }

    public void SendStateToUI(MusicPlayerUi ui)
    {
        var state = new MusicPlayerUiState(
            currentTrack: _currentTrackName,
            position: CurrentPlaybackPosition,
            duration: GetTrackDuration(),
            isPlaying: IsPlaying,
            volume: _volume,
            shuffleEnabled: _shuffleEnabled
        );
        ui.UpdateState(state);
    }

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Shutdown()
    {
        StopTrack();
        _activeUIs.Clear();
        base.Shutdown();
    }

    private float GetAudioFileDuration(string filePath)
    {
        if (_durationCache.TryGetValue(filePath, out var cachedDuration))
            return cachedDuration;

        float duration = 180.0f;
        try
        {
            if (_resMan.TryContentFileRead(filePath, out var fileStream))
            {
                using (fileStream)
                {
                    var audioStream = _audioManager.LoadAudioOggVorbis(fileStream, filePath);
                    if (audioStream != null)
                    {
                        duration = (float)audioStream.Length.TotalSeconds;
                        audioStream.Dispose();
                    }
                }
            }
        }
        catch { }
        _durationCache[filePath] = duration;
        return duration;
    }
}
