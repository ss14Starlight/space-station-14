using Content.Server.Popups;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Audio;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Keeps a <see cref="TutorialMentorMode.Holopad"/> coach beside the player by re-projecting them
/// at the holopad of whichever chamber the player is in, rather than walking them there.
/// </summary>
public sealed partial class TutorialHoloMentorSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;
    [Dependency] private TutorialTrainerSystem _trainer = default!;

    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier ProjectSound =
        new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    /// <summary>
    /// How much nearer a rival pad must be before she bothers moving, in tiles. Small enough to
    /// read as "always the closest one" — it exists only so a player parked on the exact midpoint
    /// between two pads cannot make her flicker between them twice a second.
    /// </summary>
    private const float MinFollowGain = 0.35f;

    private static readonly TimeSpan FollowInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _nextFollowCheck;

    /// <summary>
    /// Re-projects the coach into the chamber the player's current goal expects, if they are not
    /// already there. Called from <c>TutorialServerRuleSystem.RefreshParticipantHud</c> alongside
    /// the walking mentor's catch-up — <c>TutorialGuideSystem</c> already owns the directed
    /// progress-changed subscription, and Robust allows only one per component/event pair.
    /// </summary>
    public void RefreshProjection(EntityUid player, TutorialRolePrototype role, TutorialSessionData session)
    {
        if (role.MentorMode != TutorialMentorMode.Holopad)
            return;

        if (session.MentorUid == EntityUid.Invalid || TerminatingOrDeleted(session.MentorUid))
            return;

        var room = _tutorial.ResolveCurrentRoom(role, session);
        if (!TryFindHoloPoint(player, room, out var pad, out var padDistance))
            return;

        // Keyed on the pad rather than the chamber: a long chamber can hold several, and she
        // moves to whichever is nearest as the player works through its sub-goals instead of
        // staying at the entrance talking to an empty room.
        var current = session.MentorHoloPad;
        if (pad == current && !TerminatingOrDeleted(pad))
            return;

        var sameChamber = room == session.MentorHoloRoom;

        // Within one chamber she tracks whichever pad is nearest, subject only to the anti-flicker
        // margin. Changing chamber is not subject to it at all: the next chamber's pad is by
        // definition further away than the one she is standing on, and holding her back would leave
        // her delivering the next room's briefing from the room the player is about to leave.
        if (sameChamber &&
            Exists(current) && !TerminatingOrDeleted(current) &&
            TryGetPlayerDistance(player, current, out var currentDistance) &&
            padDistance > currentDistance - MinFollowGain)
        {
            return;
        }

        session.MentorHoloPad = pad;
        session.MentorHoloRoom = room;

        // Sliding to a nearer pad in the same room is not an arrival — she is already mid-
        // conversation with this player, so it gets neither the chime nor a fresh range check.
        // Only a new chamber makes them walk up to her again.
        Reproject(session.MentorUid, pad, Transform(player).MapUid, announce: !sameChamber);

        if (!sameChamber)
            _trainer.ResetArrival(session.MentorUid);
    }

    /// <summary>
    /// Follows the player between pads inside a chamber.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextFollowCheck)
            return;

        _nextFollowCheck = now + FollowInterval;

        var query = EntityQueryEnumerator<TutorialParticipantComponent>();
        while (query.MoveNext(out var player, out _))
        {
            if (!_tutorial.TryGetSession(player, out var session) ||
                !_tutorial.TryGetRole(player, out var role))
                continue;

            RefreshProjection(player, role, session);
        }
    }

    /// <summary>
    /// Ends the projection: every pad goes dark and the coach vanishes from the last one.
    /// </summary>
    public void EndProjection(TutorialSessionData session, EntityUid? mapUid)
    {
        var query = EntityQueryEnumerator<TutorialHoloPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (mapUid != null && xform.MapUid != mapUid)
                continue;

            SetPadActive(uid, false);
        }

        session.MentorHoloPad = EntityUid.Invalid;
        session.MentorHoloRoom = -1;

        var mentor = session.MentorUid;
        if (mentor == EntityUid.Invalid || TerminatingOrDeleted(mentor))
            return;

        QueueDel(mentor);
        session.MentorUid = EntityUid.Invalid;
    }

    private bool TryGetPlayerDistance(EntityUid player, EntityUid pad, out float distance)
    {
        distance = float.MaxValue;

        var playerXform = Transform(player);
        var padXform = Transform(pad);
        if (playerXform.MapUid == null || playerXform.MapUid != padXform.MapUid)
            return false;

        distance = (_transform.GetWorldPosition(padXform) - _transform.GetWorldPosition(playerXform)).Length();
        return true;
    }

    /// <summary>
    /// Finds the pad to project from: the nearest pad on the player's map whose
    /// <see cref="TutorialHoloPointComponent.Room"/> matches this chamber.
    /// </summary>
    private bool TryFindHoloPoint(EntityUid player, int room, out EntityUid pad, out float distance)
    {
        pad = EntityUid.Invalid;
        distance = float.MaxValue;

        var playerXform = Transform(player);
        var mapUid = playerXform.MapUid;
        if (mapUid == null)
            return false;

        var playerPos = _transform.GetWorldPosition(playerXform);

        var best = float.MaxValue;
        var bestPad = EntityUid.Invalid;

        var query = EntityQueryEnumerator<TutorialHoloPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var point, out var xform))
        {
            if (xform.MapUid != mapUid || point.Room != room)
                continue;

            var distanceSq = (_transform.GetWorldPosition(xform) - playerPos).LengthSquared();
            if (distanceSq >= best)
                continue;

            best = distanceSq;
            bestPad = uid;
        }

        if (best >= float.MaxValue)
            return false;

        pad = bestPad;
        distance = MathF.Sqrt(best);
        return true;
    }

    private void Reproject(EntityUid mentor, EntityUid pad, EntityUid? mapUid, bool announce)
    {
        _transform.SetCoordinates(mentor, Transform(pad).Coordinates);

        // Exactly one projector runs at a time, so the rooms she has left go dark behind her.
        var query = EntityQueryEnumerator<TutorialHoloPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            SetPadActive(uid, uid == pad);
        }

        if (!announce)
            return;

        _audio.PlayPvs(ProjectSound, mentor);
        _popup.PopupEntity(
            Loc.GetString("tutorial-holo-mentor-reproject"),
            mentor,
            PopupType.Medium);
    }

    private void SetPadActive(EntityUid pad, bool active)
    {
        _appearance.SetData(pad, TutorialHoloPointVisuals.Active, active);
        _pointLight.SetEnabled(pad, active);
        _ambient.SetAmbience(pad, active);

        // A pad that was projecting on its own takes its hologram down with it. Nothing else would:
        // she is not the session's mentor, so none of the coach teardown reaches her.
        if (active || !TryComp<TutorialHoloPointComponent>(pad, out var point))
            return;

        if (point.Projection is { } projection && !TerminatingOrDeleted(projection))
            QueueDel(projection);

        point.Projection = null;
    }
}
