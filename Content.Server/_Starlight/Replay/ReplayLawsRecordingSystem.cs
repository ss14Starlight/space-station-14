using System.Linq;
using Content.Server._Starlight.Thaven;
using Content.Server.Silicons.Laws;
using Content.Shared._Starlight.Replay;
using Content.Shared._Starlight.Thaven;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Replays;

namespace Content.Server._Starlight.Replay;

/// <summary>
/// Keeps <see cref="ReplayLawsComponent"/> current while a replay is recording.
/// </summary>
/// <remarks>
/// Polled because law changes raise no event. GetLaws is the same call made whenever a silicon opens its laws.
/// </remarks>
public sealed partial class ReplayLawsRecordingSystem : EntitySystem
{
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private SiliconLawSystem _siliconLaws = default!;
    [Dependency] private ThavenMoodsSystem _thavenMoods = default!;

    private const float UpdateInterval = 2f;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplayLawsComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);
    }

    // Real players have a session; the replay recorder doesn't.
    private void OnGetStateAttempt(Entity<ReplayLawsComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player != null)
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_replay.IsRecording)
            return;

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        var silicons = EntityQueryEnumerator<SiliconLawBoundComponent>();
        while (silicons.MoveNext(out var uid, out var bound))
        {
            var laws = _siliconLaws.GetLaws(uid, bound).Laws;
            var comp = EnsureComp<ReplayLawsComponent>(uid);

            if (comp.SiliconLaws != null && comp.SiliconLaws.SequenceEqual(laws))
                continue;

            comp.SiliconLaws = laws.Select(law => law.ShallowClone()).ToList();
            Dirty(uid, comp);
        }

        var shared = _thavenMoods.SharedMoods;
        var thavens = EntityQueryEnumerator<ThavenMoodsComponent>();
        while (thavens.MoveNext(out var uid, out var moods))
        {
            var comp = EnsureComp<ReplayLawsComponent>(uid);
            IReadOnlyList<ThavenMood> current = moods.FollowsSharedMoods ? shared : [];

            // Shared moods are replaced rather than edited, so reference equality catches changes.
            if (comp.SharedMoods != null && comp.SharedMoods.SequenceEqual(current))
                continue;

            comp.SharedMoods = current.ToList();
            Dirty(uid, comp);
        }
    }
}
