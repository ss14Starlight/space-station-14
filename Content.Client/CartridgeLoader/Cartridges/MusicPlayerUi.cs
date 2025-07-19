using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;

namespace Content.Client.CartridgeLoader.Cartridges;

[Virtual]
public sealed partial class MusicPlayerUi : UIFragment
{
    private MusicPlayerUiFragment? _fragment;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new MusicPlayerUiFragment();
        var musicSystem = _fragment.MusicSystem;
        if (musicSystem != null)
        {
            musicSystem.RegisterUI(this);
            _fragment.UpdateNowPlaying(musicSystem.CurrentTrackName, musicSystem.IsPlaying);
            _fragment.UpdateProgress(musicSystem.CurrentPlaybackPosition, musicSystem.CurrentTrackDuration);
            _fragment.UpdatePlaybackButtons(musicSystem.IsPlaying);
            _fragment.UpdateShuffleButton(musicSystem.ShuffleEnabled);
        }

        _fragment.OnPlayPressed += () =>
        {
            var selectedIndex = _fragment.GetSelectedTrackIndex();
            if (selectedIndex >= 0)
            {
                var trackPath = _fragment.GetTrackPath(selectedIndex);
                var trackName = _fragment.GetTrackName(selectedIndex);
                if (!string.IsNullOrEmpty(trackPath) && musicSystem != null)
                {
                    musicSystem.PlayTrack(trackPath, trackName);
                    _fragment.UpdatePlaybackButtons(true);
                    _fragment.UpdateNowPlaying(trackName, true);
                }
            }
        };

        _fragment.OnPausePressed += () =>
        {
            if (musicSystem != null)
            {
                musicSystem.PauseTrack();
                _fragment.UpdatePlaybackButtons(false);
            }
        };

        _fragment.OnStopPressed += () =>
        {
            if (musicSystem != null)
            {
                musicSystem.StopTrack();
                _fragment.UpdatePlaybackButtons(false);
                _fragment.UpdateNowPlaying("", false);
            }
        };

        _fragment.OnSeekRequested += position => musicSystem?.SetPosition(position);
        _fragment.OnVolumeChanged += volume => musicSystem?.SetVolume(volume);
        _fragment.OnSkipPressed += () => musicSystem?.PlayNextTrack();
        _fragment.OnShufflePressed += () =>
        {
            if (musicSystem != null)
            {
                musicSystem.ToggleShuffle();
                _fragment.UpdateShuffleButton(musicSystem.ShuffleEnabled);
            }
        };
    }

    public void Dispose()
    {
        var musicSystem = _fragment?.MusicSystem;
        musicSystem?.UnregisterUI(this);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not MusicPlayerUiState musicState)
            return;
        _fragment?.UpdateState(musicState);
    }

    public void UpdateUI()
    {
        _fragment?.UpdateUI();
    }
}
