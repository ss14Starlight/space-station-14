using System.Numerics;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Turns a standing mentor to face whoever he is talking to.
/// </summary>
/// <remarks>
/// Rotation is only ever written by <c>SharedMoverController</c>, and only on a tick the entity is
/// moving, so a mentor keeps whatever direction his last step left him in — which for a coach who
/// walks somewhere and then delivers a paragraph is his back. Almost always that is the player, so
/// that is the default; the beats where it is not are authored on
/// <see cref="TutorialMentorComponent.Facing"/>, because the only thing that knows he is talking to
/// the Head of Personnel rather than to the player is the script.
/// </remarks>
public sealed partial class TutorialMentorFacingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RotateToFaceSystem _rotate = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;

    /// <summary>
    /// Below this he counts as standing still. Not zero: a mob resting against a wall keeps a
    /// little residual velocity, and a coach who never quite stops is a coach who never looks up.
    /// </summary>
    private const float StandingStillSpeedSquared = 0.01f;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.25);

    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<TutorialMentorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mentor, out var xform))
        {
            var player = mentor.PlayerUid;
            if (player == EntityUid.Invalid || TerminatingOrDeleted(player))
                continue;

            // A projected coach is where she is projected and faces the way the pad faces. Her
            // sprite is a hologram layer with no directions in it, so turning her would tip the
            // whole image over rather than turn her head.
            if (xform.Anchored)
                continue;

            // Walking already points him where he is going, and fighting the mover for the same
            // field would only make him stutter.
            if (TryComp<PhysicsComponent>(uid, out var physics) &&
                physics.LinearVelocity.LengthSquared() > StandingStillSpeedSquared)
            {
                continue;
            }

            if (!TryResolveTarget(uid, mentor, player, xform, out var target))
                continue;

            _rotate.TryFaceCoordinates(uid, target, xform);
        }
    }

    /// <summary>
    /// Where he should be looking: the beat's own target if it names one and it is here, and the
    /// player otherwise. Falling back rather than giving up matters for the staged beats, whose
    /// target walks into the scene partway through the script he is facing them for.
    /// </summary>
    private bool TryResolveTarget(
        EntityUid mentor,
        TutorialMentorComponent comp,
        EntityUid player,
        TransformComponent xform,
        out Vector2 target)
    {
        target = default;

        if (TryResolveFacingTag(mentor, comp, player, xform, out var tagged))
        {
            target = _transform.GetWorldPosition(Transform(tagged));
            return true;
        }

        var playerXform = Transform(player);
        if (playerXform.MapID != xform.MapID)
            return false;

        target = _transform.GetWorldPosition(playerXform);
        return true;
    }

    /// <summary>
    /// Nearest entity carrying the tag this beat faces, on the mentor's own grid so one player's
    /// scene cannot turn him toward another player's copy of it.
    /// </summary>
    private bool TryResolveFacingTag(
        EntityUid mentor,
        TutorialMentorComponent comp,
        EntityUid player,
        TransformComponent xform,
        out EntityUid tagged)
    {
        tagged = EntityUid.Invalid;

        if (comp.Facing.Count == 0)
            return false;

        if (!TryComp<TutorialParticipantComponent>(player, out var part) ||
            !_tutorial.TryGetCurrentSubGoal(player, part, out var sub))
        {
            return false;
        }

        string? wanted = null;
        foreach (var facing in comp.Facing)
        {
            if (!string.Equals(facing.SubGoalId, sub.Id, StringComparison.Ordinal))
                continue;

            wanted = facing.Tag;
            break;
        }

        if (string.IsNullOrEmpty(wanted))
            return false;

        var tag = (ProtoId<TagPrototype>) wanted;
        var origin = _transform.GetWorldPosition(xform);
        var best = float.MaxValue;

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var candidate))
        {
            if (uid == mentor || candidate.GridUid != xform.GridUid || !_tags.HasTag(uid, tag))
                continue;

            var distance = Vector2.DistanceSquared(origin, _transform.GetWorldPosition(candidate));
            if (distance >= best)
                continue;

            best = distance;
            tagged = uid;
        }

        return tagged != EntityUid.Invalid;
    }
}
