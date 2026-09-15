using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Functional.TutorialServer;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Walks a <see cref="TutorialMentorMode.Lead"/> coach to the <see cref="TutorialWalkPointComponent"/>
/// of the room the curriculum is in, and leaves him there.
/// </summary>
/// <remarks>
/// The inverse of <see cref="TutorialMentorFollowSystem"/>, and the reason the two are separate
/// systems rather than one with a flag: a following coach is always beside the player and so can
/// talk whenever he likes, where a leading coach is somewhere the player is not yet. That makes
/// his trainer's <see cref="TutorialTrainerComponent.SpeakRange"/> load-bearing — it is what turns
/// "he walked off" into "follow him", because the next thing he has to say does not start until
/// the player has caught up. The other half of that is
/// <see cref="TutorialTrainerComponent.SpeechHeld"/>: he is silent for the length of the walk, so
/// a section starts where it happens rather than being narrated over his shoulder on the way.
/// Arrival is cleared on every room change for the same reason a holopad coach clears it when she
/// re-projects: a new room is a new walk.
/// </remarks>
public sealed partial class TutorialLeadMentorSystem : EntitySystem
{
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;
    [Dependency] private TutorialTrainerSystem _trainer = default!;

    /// <summary>Close enough to count as standing at the point, in tiles.</summary>
    private const float ArrivalRange = 1.5f;

    /// <summary>
    /// How long he is given to walk it before he is put there. Generous on purpose: being able to
    /// watch him walk is the whole point, and a teleport is a bug the player can see. It only ever
    /// fires when something has genuinely gone wrong — a door that did not open, a body in the way.
    /// </summary>
    private static readonly TimeSpan WalkGrace = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<TutorialParticipantComponent>();
        while (query.MoveNext(out var player, out _))
        {
            if (!_tutorial.TryGetSession(player, out var session) ||
                !_tutorial.TryGetRole(player, out var role))
            {
                continue;
            }

            RefreshLead(player, role, session);
        }
    }

    /// <summary>
    /// Points the coach at the room the curriculum is now in, if that is somewhere new.
    /// </summary>
    public void RefreshLead(EntityUid player, TutorialRolePrototype role, TutorialSessionData session)
    {
        if (role.MentorMode != TutorialMentorMode.Lead)
            return;

        var mentor = session.MentorUid;
        if (mentor == EntityUid.Invalid || TerminatingOrDeleted(mentor))
            return;

        // Beats that want him watching rather than walking. He keeps whatever destination he
        // already had, so a hold placed mid-walk does not strand him halfway.
        if (TryComp<TutorialParticipantComponent>(player, out var part) &&
            _tutorial.TryGetCurrentSubGoal(player, part, out var current) &&
            current.MentorHolds)
            return;

        var room = _tutorial.ResolveCurrentRoom(role, session);
        if (!TryFindWalkPoint(player, room, out var point))
            return;

        var moving = point != session.MentorWalkPoint || TerminatingOrDeleted(session.MentorWalkPoint);

        // Held the moment he has somewhere else to be, not when his feet finally move. Between
        // those two sits the pause owed to his last line, and leaving speech free through it let
        // the whole of the next section play before he had taken a step: every "come and look at
        // this" arriving while he was still standing in the room before it.
        //
        // No exemption for the first walk. There used to be one, on the grounds that the line
        // asking the player to follow lived in that opening segment and holding it would leave
        // them alone with a departing stranger. It does not any more: the arrivals beat pins him
        // in front of them to say it. The exemption survived as a hole exactly one walk wide, and
        // the walk it applied to was the one to the dorms.
        if (moving)
            _trainer.HoldSpeech(mentor, true);

        // Then let the line already on screen finish before he turns to go.
        if (TryComp<TutorialTrainerComponent>(mentor, out var speech) &&
            _timing.CurTime < speech.SpeakingUntil)
        {
            return;
        }

        if (moving)
        {
            var newRoom = room != session.MentorWalkRoom;

            session.MentorWalkPoint = point;
            session.MentorWalkRoom = room;
            SetDestination(mentor, point);

            // A new walk gets the whole grace window. Leaving the old one running teleported him
            // the instant he was released, because the clock had been ticking through the hold.
            if (TryComp<TutorialMentorComponent>(mentor, out var walking))
                walking.CatchUpDeadline = null;

            // Only a new room makes the player walk up to him again. Moving between points inside
            // one room is him drifting to the next thing, not a fresh arrival.
            if (newRoom)
                _trainer.ResetArrival(mentor);
        }

        KeepWalking(mentor, point);
    }

    /// <summary>
    /// Watches the walk in and puts him there if it never finishes.
    /// </summary>
    private void KeepWalking(EntityUid mentor, EntityUid point)
    {
        if (!TryComp<TutorialMentorComponent>(mentor, out var comp))
            return;

        if (IsAtPoint(mentor, point))
        {
            comp.CatchUpDeadline = null;
            _trainer.HoldSpeech(mentor, false);
            return;
        }

        if (comp.CatchUpDeadline is not { } deadline)
        {
            comp.CatchUpDeadline = _timing.CurTime + WalkGrace;
            return;
        }

        if (_timing.CurTime < deadline)
            return;

        comp.CatchUpDeadline = null;
        _transform.SetCoordinates(mentor, Transform(point).Coordinates);
        SetDestination(mentor, point);

        // The teleport is an arrival too, or a walk that never finished would leave him standing
        // in the right place with the rest of the curriculum stuck behind him.
        _trainer.HoldSpeech(mentor, false);
    }

    private bool IsAtPoint(EntityUid mentor, EntityUid point)
    {
        var mentorXform = Transform(mentor);
        var pointXform = Transform(point);
        if (mentorXform.MapUid == null || mentorXform.MapUid != pointXform.MapUid)
            return false;

        var delta = _transform.GetWorldPosition(pointXform) - _transform.GetWorldPosition(mentorXform);
        return delta.Length() <= ArrivalRange;
    }

    /// <summary>
    /// Nearest walk point on the player's map that serves <paramref name="room"/>. Nearest rather
    /// than first so a room may hold several and he uses whichever the beat has taken him toward.
    /// </summary>
    private bool TryFindWalkPoint(EntityUid player, int room, out EntityUid point)
    {
        point = EntityUid.Invalid;

        var playerXform = Transform(player);
        var mapUid = playerXform.MapUid;
        if (mapUid == null)
            return false;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var best = float.MaxValue;

        var query = EntityQueryEnumerator<TutorialWalkPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var walk, out var xform))
        {
            if (xform.MapUid != mapUid || walk.Room != room)
                continue;

            var distanceSq = (_transform.GetWorldPosition(xform) - playerPos).LengthSquared();
            if (distanceSq >= best)
                continue;

            best = distanceSq;
            point = uid;
        }

        return point != EntityUid.Invalid;
    }

    private void SetDestination(EntityUid mentor, EntityUid point)
    {
        var target = new EntityCoordinates(point, Vector2.Zero);

        if (TryComp<HTNComponent>(mentor, out var htn))
        {
            _npc.SetBlackboard(mentor, NPCBlackboard.FollowTarget, target, htn);
            _htn.Replan(htn);
            return;
        }

        _npc.SetBlackboard(mentor, NPCBlackboard.FollowTarget, target);
    }
}
