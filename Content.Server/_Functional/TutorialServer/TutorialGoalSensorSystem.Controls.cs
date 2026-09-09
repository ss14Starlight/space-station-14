using Content.Shared._Functional.TutorialServer;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Pointing;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Sensors for the basic-controls curriculum: movement, gait, posture, climbing, buckling,
/// camera and pointing. These teach the controls themselves, so they watch raw player state
/// rather than practice props.
/// </summary>
public sealed partial class TutorialGoalSensorSystem
{
    // [Dependency] private IGameTiming _controlsTiming = default!;
    [Dependency] private TutorialTrainerSystem _trainer = default!;

    /// <summary>
    /// Longest the control hint waits on the coach before showing anyway.
    /// </summary>
    private const float ControlHintFallbackSeconds = 15f;

    private void InitializeControls()
    {
        SubscribeLocalEvent<TutorialParticipantComponent, MoveInputEvent>(OnPlayerMoveInput);
        SubscribeLocalEvent<TutorialParticipantComponent, KnockedDownEvent>(OnPlayerKnockedDown);
        SubscribeLocalEvent<TutorialParticipantComponent, StoodEvent>(OnPlayerStood);
        SubscribeLocalEvent<TutorialParticipantComponent, StartClimbEvent>(OnPlayerClimbed);
        SubscribeLocalEvent<TutorialParticipantComponent, BuckledEvent>(OnPlayerBuckled);
        SubscribeLocalEvent<TutorialParticipantComponent, UnbuckledEvent>(OnPlayerUnbuckled);
        SubscribeLocalEvent<TutorialParticipantComponent, AfterPointedAtEvent>(OnPlayerPointed);
    }

    /// <summary>
    /// Polled control sensors. Discrete actions are event-driven above; these are state checks
    /// that must also pass when the player already satisfies them as the sub-goal becomes current.
    /// </summary>
    private void UpdateControlSensors(EntityUid uid, TutorialSubGoalData sub)
    {
        switch (sub.Complete)
        {
            case TutorialStepComplete.PlayerCrawling:
                if (IsCrawling(uid))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.PlayerStanding:
                if (!IsCrawling(uid))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.PlayerBuckled:
                if (TryComp<BuckleComponent>(uid, out var buckle) &&
                    buckle.BuckledTo is { } strap &&
                    MatchesOptionalTag(strap, sub.Tag))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.PlayerUnbuckled:
                if (!TryComp<BuckleComponent>(uid, out var unbuckle) || unbuckle.BuckledTo == null)
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.CameraRotated:
                if (TryComp<InputMoverComponent>(uid, out var rotMover) &&
                    !rotMover.TargetRelativeRotation.Equals(Angle.Zero))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.CameraResetDone:
                // Only reachable after CameraRotated, so a zeroed camera means the player reset it.
                if (TryComp<InputMoverComponent>(uid, out var resetMover) &&
                    resetMover.TargetRelativeRotation.Equals(Angle.Zero))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.Acknowledge:
                TryAutoAdvanceNarration(uid, sub);
                break;
        }
    }

    /// <summary>
    /// Narration beats land before the player knows how to click anything, so an authored
    /// <see cref="TutorialSubGoalData.AutoAdvanceSeconds"/> lets them play out on a timer.
    /// Clicking the coach still skips ahead via <c>TutorialTrainerSystem</c>.
    /// </summary>
    private void TryAutoAdvanceNarration(EntityUid uid, TutorialSubGoalData sub)
    {
        if (sub.AutoAdvanceSeconds is not { } seconds)
            return;

        // Never talk over the coach: a narration beat ends when she has finished the segment,
        // however long the player took to walk into earshot.
        if (IsCoachStillSpeaking(uid))
            return;

        if (!_tutorial.TryGetSubGoalElapsed(uid, out var elapsed))
            return;

        if (elapsed.TotalSeconds < seconds)
            return;

        _tutorial.AdvanceSubGoal(uid);
    }

    private bool IsCoachStillSpeaking(EntityUid uid)
        => ResolveCoachSpeech(uid) != TutorialCoachSpeech.Done;

    private TutorialCoachSpeech ResolveCoachSpeech(EntityUid uid)
    {
        if (!_tutorial.TryGetSession(uid, out var session))
            return TutorialCoachSpeech.Done;

        // No mentor is not the same as nobody talking: a beat can be handed to a second voice, and
        // ResolveBeatSpeech is what sees it.
        var mentor = session.MentorUid;

        if (!TryComp<TutorialParticipantComponent>(uid, out var part) ||
            !_tutorial.TryGetCurrentSubGoal(uid, part, out var sub))
            return TutorialCoachSpeech.Done;

        return _trainer.ResolveBeatSpeech(uid, mentor, sub.Id);
    }

    /// <summary>
    /// Releases the sub-goal's control hint once the coach has said her piece.
    /// </summary>
    /// <remarks>
    /// The timeout is the safety net for a coach the player never walks up to, so it only applies
    /// while she is waiting to start. Timing out a segment already in progress put the banner up in
    /// the middle of what she was saying, which is what it was meant to stay out of the way of.
    /// </remarks>
    private void TryReleaseControlHint(EntityUid uid)
    {
        if (!_tutorial.HasPendingControlHint(uid))
            return;

        switch (ResolveCoachSpeech(uid))
        {
            case TutorialCoachSpeech.Speaking:
                return;
            case TutorialCoachSpeech.Waiting
                when !_tutorial.TryGetSubGoalElapsed(uid, out var elapsed) ||
                     elapsed.TotalSeconds < ControlHintFallbackSeconds:
                return;
        }

        _tutorial.ShowPendingControlHint(uid);
    }

    /// <summary>
    /// Reaching a sub-goal's <see cref="TutorialSubGoalData.RetryMarker"/> means the player got to
    /// the end of the drill without ever doing it. Nothing in the completion conditions can see
    /// that, so it is checked positionally.
    /// </summary>
    private void TryFailAtRetryMarker(EntityUid uid, TransformComponent xform, TutorialSubGoalData sub)
    {
        if (string.IsNullOrEmpty(sub.RetryMarker))
            return;

        if (!IsAtMarker(xform, sub.RetryMarker))
            return;

        FailDrill(uid, sub);
    }

    /// <summary>
    /// Fails a drill: the coach says so, and the player is put back where it starts.
    /// </summary>
    /// <remarks>
    /// The return is what makes it foolproof. Telling the player to walk back only works on
    /// players who are listening, and the whole point of these beats is the ones who are not.
    /// </remarks>
    private void FailDrill(EntityUid uid, TutorialSubGoalData sub)
    {
        if (!_tutorial.TryGetSession(uid, out var session))
            return;

        if (sub.RetryLine is { } line)
        {
            var mentor = session.MentorUid;
            if (mentor != EntityUid.Invalid && !TerminatingOrDeleted(mentor))
                _trainer.TrySpeakInterjection(mentor, uid, line);
        }

        if (string.IsNullOrEmpty(sub.RetryReturnMarker))
            return;

        var xform = Transform(uid);
        if (!TryGetMarkerCoords(xform.MapID, sub.RetryReturnMarker, out var coords))
            return;

        // Moving them off the failure marker is also what stops this repeating every tick.
        _transform.SetCoordinates(uid, coords);
    }

    private void OnPlayerMoveInput(Entity<TutorialParticipantComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (!_tutorial.TryGetCurrentSubGoal(ent, ent.Comp, out var sub))
            return;

        switch (sub.Complete)
        {
            case TutorialStepComplete.PlayerMoved:
                _tutorial.AdvanceSubGoal(ent);
                break;
            case TutorialStepComplete.PlayerWalking when !args.Entity.Comp.Sprinting:
                _tutorial.AdvanceSubGoal(ent);
                break;
        }
    }

    private void OnPlayerKnockedDown(Entity<TutorialParticipantComponent> ent, ref KnockedDownEvent args)
    {
        AdvanceIfCurrent(ent, TutorialStepComplete.PlayerCrawling);

        // Slipping on the peels breaks the walk drill; say so rather than leaving them to work out
        // why arriving at the marker did nothing.
        if (_tutorial.TryGetCurrentSubGoal(ent, ent.Comp, out var sub) &&
            sub.Complete == TutorialStepComplete.ReachMarker &&
            sub.Posture == TutorialPosture.Walking)
        {
            FailDrill(ent, sub);
        }
    }

    private void OnPlayerStood(EntityUid uid, TutorialParticipantComponent comp, StoodEvent args)
    {
        AdvanceIfCurrent((uid, comp), TutorialStepComplete.PlayerStanding);
    }

    private void OnPlayerClimbed(Entity<TutorialParticipantComponent> ent, ref StartClimbEvent args)
    {
        if (!_tutorial.TryGetCurrentSubGoal(ent, ent.Comp, out var sub))
            return;

        if (sub.Complete != TutorialStepComplete.PlayerClimbed)
            return;

        if (!MatchesOptionalTag(args.Climbable, sub.Tag))
            return;

        _tutorial.AdvanceSubGoal(ent);
    }

    private void OnPlayerBuckled(Entity<TutorialParticipantComponent> ent, ref BuckledEvent args)
    {
        if (!_tutorial.TryGetCurrentSubGoal(ent, ent.Comp, out var sub))
            return;

        if (sub.Complete != TutorialStepComplete.PlayerBuckled)
            return;

        if (!MatchesOptionalTag(args.Strap, sub.Tag))
            return;

        _tutorial.AdvanceSubGoal(ent);
    }

    private void OnPlayerUnbuckled(Entity<TutorialParticipantComponent> ent, ref UnbuckledEvent args)
    {
        AdvanceIfCurrent(ent, TutorialStepComplete.PlayerUnbuckled);
    }

    private void OnPlayerPointed(Entity<TutorialParticipantComponent> ent, ref AfterPointedAtEvent args)
    {
        if (!_tutorial.TryGetCurrentSubGoal(ent, ent.Comp, out var sub))
            return;

        if (sub.Complete != TutorialStepComplete.PlayerPointed)
            return;

        if (!MatchesOptionalTag(args.Pointed, sub.Tag))
            return;

        _tutorial.AdvanceSubGoal(ent);
    }

    private void AdvanceIfCurrent(Entity<TutorialParticipantComponent> ent, TutorialStepComplete expected)
    {
        if (!_tutorial.TryGetCurrentSubGoal(ent, ent.Comp, out var sub))
            return;

        if (sub.Complete != expected)
            return;

        _tutorial.AdvanceSubGoal(ent);
    }

    /// <summary>
    /// True when the sub-goal did not specify a tag, or the target carries it.
    /// </summary>
    private bool MatchesOptionalTag(EntityUid target, string? tag)
    {
        return string.IsNullOrEmpty(tag) || _tags.HasTag(target, tag);
    }

    private bool IsCrawling(EntityUid uid)
    {
        return HasComp<KnockedDownComponent>(uid) ||
               (TryComp<StandingStateComponent>(uid, out var standing) && !standing.Standing);
    }

    /// <summary>
    /// Gate for posture-qualified sub-goals (currently <see cref="TutorialStepComplete.ReachMarker"/>),
    /// so one marker beat can require the player to arrive walking or crawling.
    /// </summary>
    private bool MatchesPosture(EntityUid uid, TutorialPosture posture)
    {
        switch (posture)
        {
            case TutorialPosture.Any:
                return true;
            case TutorialPosture.Crawling:
                return IsCrawling(uid);
            case TutorialPosture.Standing:
                return !IsCrawling(uid);
            case TutorialPosture.Walking:
                return !IsCrawling(uid) &&
                       TryComp<InputMoverComponent>(uid, out var mover) &&
                       !mover.Sprinting;
            default:
                return true;
        }
    }
}
