using System.Linq;
using Content.Shared._Starlight.Replay;
using Robust.Server.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Replays;

namespace Content.Server._Starlight.Replay;

/// <summary>
/// Records the audience of private sounds while a replay is recording.
/// </summary>
/// <remarks>
/// Sounds passed with recordReplay (announcements) or sent to everyone in game stay public in replays.
/// </remarks>
public sealed partial class ReplayAudioAudienceSystem : EntitySystem
{
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AudioComponent, AudioFilterAppliedEvent>(OnAudioFiltered);
        SubscribeLocalEvent<ReplayAudioAudienceComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);
    }

    private void OnAudioFiltered(Entity<AudioComponent> ent, ref AudioFilterAppliedEvent args)
    {
        if (!_replay.IsRecording || args.RecordReplay || IsBroadcast(args.Filter)
            || ent.Comp.IncludedEntities is not { } included)
        {
            return;
        }

        var audience = EnsureComp<ReplayAudioAudienceComponent>(ent);
        audience.Audience = new HashSet<EntityUid>(included);
        Dirty(ent, audience);
    }

    // Broadcast if every in-game player hears it. Filters can also hold lobby sessions, so counts alone can't tell.
    // With one player in game, every filter looks like a broadcast, so that case counts as private.
    private bool IsBroadcast(Filter filter)
    {
        var inGame = 0;
        foreach (var session in _player.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            if (!filter.Recipients.Contains(session))
                return false;

            inGame++;
        }

        return inGame > 1;
    }

    // Real players have a session; the replay recorder doesn't.
    private void OnGetStateAttempt(Entity<ReplayAudioAudienceComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player != null)
            args.Cancelled = true;
    }
}
