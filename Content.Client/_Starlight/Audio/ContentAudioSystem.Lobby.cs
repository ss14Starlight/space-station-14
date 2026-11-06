using Content.Shared.GameTicking;
using Robust.Shared.Audio.Components;

// ReSharper disable CheckNamespace
namespace Content.Client.Audio;

public sealed partial class ContentAudioSystem
{
    private void OnRoundEndCancelMessage(RoundEndCancelMessageEvent ev)
    {
        _postRoundMusic = false;
        EndLobbyMusic();
    }

    private bool _postRoundMusic;

    private bool IsLobbyMusicExisting(LobbySoundtrackInfo info)
    {
        if (TerminatingOrDeleted(info.MusicStreamEntityUid)
            || !TryComp(info.MusicStreamEntityUid, out AudioComponent? audio))
            return true;

        return audio is { Started: true, Playing: false };
    }
}
