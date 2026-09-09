using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Interaction;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Keeps mentor FollowTarget on the coached player. On separation, gives the mentor time to
/// walk, then path-checks before teleporting — snaps only when stuck with no path and not
/// already in short range with line of sight.
/// </summary>
public sealed partial class TutorialMentorFollowSystem : EntitySystem
{
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private PathfindingSystem _pathfinding = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Vector2 SnapOffset = new(1.2f, 0f);
    private static readonly TimeSpan CatchUpDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Range that counts as "already near" (matches mentor FollowRange) and path goal range.
    /// </summary>
    private const float ShortRange = 3f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var mentors = EntityQueryEnumerator<TutorialMentorComponent, TransformComponent>();
        while (mentors.MoveNext(out var mentorUid, out var mentor, out var mentorXform))
        {
            if (mentor.PlayerUid == EntityUid.Invalid || TerminatingOrDeleted(mentor.PlayerUid))
                continue;

            // Stationary bay coaches (no HTN) must not follow or snap.
            if (!HasComp<HTNComponent>(mentorUid))
                continue;

            // Leading coaches are walked by TutorialLeadMentorSystem. Both systems write
            // FollowTarget, so exactly one of them may own a given mentor.
            if (mentor.Leads)
                continue;

            var playerXform = Transform(mentor.PlayerUid);

            EnsureFollowTarget(mentorUid, mentor.PlayerUid);

            var mentorMap = mentorXform.MapUid;
            var playerMap = playerXform.MapUid;
            if (mentorMap == null || playerMap == null || mentorMap != playerMap)
            {
                SnapBesidePlayer(mentorUid, mentor.PlayerUid);
                continue;
            }

            var separated = mentorXform.GridUid is { } gridUid &&
                            TryComp<TutorialRoomLayoutComponent>(gridUid, out var layout) &&
                            layout.ChamberCenters.Count > 1 &&
                            GetNearestChamberIndex(layout, mentorXform.LocalPosition) !=
                            GetNearestChamberIndex(layout, playerXform.LocalPosition);

            if (separated)
                RequestCatchUp(mentorUid, mentor);
            else if (mentor.CatchUpDeadline != null && IsNearbyWithLos(mentorUid, mentor.PlayerUid))
                ClearCatchUp(mentor);

            if (mentor.CatchUpDeadline is not { } deadline)
                continue;

            if (mentor.CatchUpPathCheckInFlight)
                continue;

            if (_timing.CurTime < deadline)
                continue;

            // Grace elapsed: skip teleport if already close with LOS.
            if (IsNearbyWithLos(mentorUid, mentor.PlayerUid))
            {
                ClearCatchUp(mentor);
                continue;
            }

            StartCatchUpPathCheck(mentorUid, mentor);
        }
    }

    /// <summary>
    /// Starts / refreshes the walk-first catch-up window (does not teleport immediately).
    /// </summary>
    /// <param name="restart">
    /// When true, resets the 5s grace even if catch-up is already active (e.g. goal change).
    /// </param>
    public void RequestCatchUp(EntityUid mentorUid, TutorialMentorComponent? mentor = null, bool restart = false)
    {
        if (!Resolve(mentorUid, ref mentor))
            return;

        if (mentor.PlayerUid == EntityUid.Invalid || TerminatingOrDeleted(mentor.PlayerUid))
            return;

        // A leading coach is meant to be ahead of the player: catching him up to them would undo
        // the walk TutorialLeadMentorSystem just sent him on. Guarded here rather than only at the
        // call site because a chamber change is exactly when both would otherwise fire.
        if (mentor.Leads)
            return;

        // Already coaching in person — no catch-up needed.
        if (IsNearbyWithLos(mentorUid, mentor.PlayerUid))
        {
            ClearCatchUp(mentor);
            return;
        }

        if (!restart && mentor.CatchUpDeadline != null)
            return;

        mentor.CatchUpGeneration++;
        mentor.CatchUpDeadline = _timing.CurTime + CatchUpDelay;
        mentor.CatchUpPathCheckInFlight = false;
        EnsureFollowTarget(mentorUid, mentor.PlayerUid, replan: true);
    }

    /// <summary>
    /// Places the mentor next to the player and forces an HTN replan.
    /// </summary>
    public void SnapBesidePlayer(EntityUid mentorUid, EntityUid playerUid)
    {
        var playerXform = Transform(playerUid);

        var coords = playerXform.Coordinates.Offset(SnapOffset);
        _transform.SetCoordinates(mentorUid, coords);

        if (TryComp<TutorialMentorComponent>(mentorUid, out var mentor))
            ClearCatchUp(mentor);

        EnsureFollowTarget(mentorUid, playerUid, replan: true);
    }

    private void StartCatchUpPathCheck(EntityUid mentorUid, TutorialMentorComponent mentor)
    {
        mentor.CatchUpPathCheckInFlight = true;
        var generation = mentor.CatchUpGeneration;
        var playerUid = mentor.PlayerUid;
        _ = RunCatchUpPathCheckAsync(mentorUid, playerUid, generation);
    }

    private async Task RunCatchUpPathCheckAsync(EntityUid mentorUid, EntityUid playerUid, int generation)
    {
        PathResultEvent? result = null;
        try
        {
            if (TerminatingOrDeleted(mentorUid) || TerminatingOrDeleted(playerUid))
                return;

            if (!TryComp(mentorUid, out TransformComponent? mentorXform) ||
                !TryComp(playerUid, out TransformComponent? playerXform))
                return;

            var flags = _pathfinding.GetFlags(mentorUid);
            result = await _pathfinding.GetPathSafe(
                mentorUid,
                mentorXform.Coordinates,
                playerXform.Coordinates,
                ShortRange,
                CancellationToken.None,
                flags);
        }
        finally
        {
            // Continuations resume on the simulation context (same as DoAfter / NPC steering).
            if (!TerminatingOrDeleted(mentorUid) &&
                TryComp<TutorialMentorComponent>(mentorUid, out var mentor) &&
                mentor.CatchUpGeneration == generation)
            {
                mentor.CatchUpPathCheckInFlight = false;

                if (result is not { Result: PathResult.Path or PathResult.PartialPath })
                {
                    // Still no route after the walk grace — and not already nearby.
                    if (!IsNearbyWithLos(mentorUid, playerUid))
                        SnapBesidePlayer(mentorUid, playerUid);
                    else
                        ClearCatchUp(mentor);
                }
                else
                {
                    // Path exists: keep walking; clear deadline so we can re-arm if still separated.
                    ClearCatchUp(mentor);
                }
            }
        }
    }

    private void EnsureFollowTarget(EntityUid mentorUid, EntityUid playerUid, bool replan = false)
    {
        if (TryComp<HTNComponent>(mentorUid, out var htn))
        {
            _npc.SetBlackboard(mentorUid, NPCBlackboard.FollowTarget,
                new EntityCoordinates(playerUid, Vector2.Zero), htn);
            if (replan)
                _htn.Replan(htn);
            return;
        }

        _npc.SetBlackboard(mentorUid, NPCBlackboard.FollowTarget,
            new EntityCoordinates(playerUid, Vector2.Zero));
    }

    private bool IsNearbyWithLos(EntityUid mentorUid, EntityUid playerUid)
    {
        return _interaction.InRangeUnobstructed(mentorUid, playerUid, range: ShortRange);
    }

    private static void ClearCatchUp(TutorialMentorComponent mentor)
    {
        mentor.CatchUpDeadline = null;
        mentor.CatchUpPathCheckInFlight = false;
    }

    private static int GetNearestChamberIndex(TutorialRoomLayoutComponent layout, Vector2 localPos)
    {
        var best = 0;
        var bestDist = float.MaxValue;
        for (var i = 0; i < layout.ChamberCenters.Count; i++)
        {
            var dist = (layout.ChamberCenters[i] - localPos).LengthSquared();
            if (dist >= bestDist)
                continue;
            bestDist = dist;
            best = i;
        }

        return best;
    }
}
