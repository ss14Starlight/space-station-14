using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Client._Starlight.Replay;

public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    // Alerts meant for live ghosts only. The server plays one copy per ghost, so a replay plays them all at once.
    private static readonly HashSet<string> _replayMutedSounds = ["/Audio/_Starlight/Misc/ghost_ping.ogg"];

    // Mute before the audio system starts a new sound, so it never plays even briefly.
    private void InitializeAudio() => UpdatesBefore.Add(typeof(SharedAudioSystem));

    private void UpdateMutedSounds()
    {
        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            // Also re-mutes after recorded state or a seek restores the volume.
            if (!_replayMutedSounds.Contains(audio.FileName) || float.IsNegativeInfinity(audio.Params.Volume))
                continue;

            _audio.SetGain(uid, 0f, audio);
        }
    }
}
