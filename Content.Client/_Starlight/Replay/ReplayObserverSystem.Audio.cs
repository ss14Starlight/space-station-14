using Content.Shared._Starlight.Replay;
using Content.Shared.Follower.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Client._Starlight.Replay;

// Global sounds meant for specific players (antag briefings, held geigers) are muted unless the viewer is spectating
// or following one of them. Positional sounds are left to distance falloff, which already limits them to anyone in range.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    // Alerts meant for live ghosts only. The server plays one copy per ghost, so a replay plays them all at once.
    private static readonly HashSet<string> _replayMutedSounds = ["/Audio/_Starlight/Misc/ghost_ping.ogg"];

    private void InitializeAudio()
    {
        // Mute before the audio system starts a new sound, so it never plays even briefly.
        UpdatesBefore.Add(typeof(SharedAudioSystem));
        SubscribeLocalEvent<ReplayAudioAudienceComponent, ComponentStartup>(OnAudienceStartup);
    }

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

    private void OnAudienceStartup(Entity<ReplayAudioAudienceComponent> ent, ref ComponentStartup args)
    {
        if (IsReplayActive && TryComp<AudioComponent>(ent, out var audio))
            UpdateAudience(ent, audio);
    }

    private void UpdateAudioAudiences()
    {
        var query = EntityQueryEnumerator<ReplayAudioAudienceComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out var audience, out var audio))
        {
            UpdateAudience((uid, audience), audio);
        }
    }

    private void UpdateAudience(Entity<ReplayAudioAudienceComponent> ent, AudioComponent audio)
    {
        // Always-muted sounds are never restored here.
        if (_replayMutedSounds.Contains(audio.FileName))
            return;

        var audible = !audio.Global || IsInAudience(ent.Comp.Audience);

        if (!audible)
        {
            // Also re-mutes after recorded state or a seek restores the volume.
            if (float.IsNegativeInfinity(audio.Params.Volume))
                return;

            ent.Comp.MutedVolume = audio.Params.Volume;
            _audio.SetGain(ent.Owner, 0f, audio);
            return;
        }

        if (ent.Comp.MutedVolume is not { } volume)
            return;

        ent.Comp.MutedVolume = null;
        _audio.SetVolume(ent.Owner, volume, audio);
    }

    // The viewer hears what the mob they're spectating or following heard.
    private bool IsInAudience(HashSet<EntityUid> audience)
    {
        if (_player.LocalEntity is not { } listener)
            return false;

        return audience.Contains(listener)
            || (TryComp<FollowerComponent>(listener, out var follower) && audience.Contains(follower.Following));
    }
}
