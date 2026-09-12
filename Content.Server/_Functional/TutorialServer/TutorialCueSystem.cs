using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Light.EntitySystems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Server.DeviceLinking.Components;
using Content.Shared.Interaction;
using Content.Shared.Audio;
using Content.Shared.Camera;
using Content.Shared.Cuffs;
using Content.Shared.Light.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Fires staged effects when a participant reaches the sub-goal that cues them. Placed in the map
/// rather than the curriculum, so the effect stays with the room it happens to.
/// </summary>
public sealed partial class TutorialCueSystem : EntitySystem
{
    [Dependency] private RotateToFaceSystem _rotate = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private ExplosionSystem _explosions = default!;
    // [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PoweredLightSystem _lights = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;
    [Dependency] private TutorialTrainerSystem _trainer = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Enough to feel structural, not enough to lose the player's cursor.</summary>
    private static readonly Vector2 BreachKick = new(0f, -0.6f);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var armed = EntityQueryEnumerator<TutorialCueComponent>();
        while (armed.MoveNext(out var uid, out var cue))
        {
            if (cue.Fired || cue.FireAt is not { } fireAt || now < fireAt)
                continue;

            cue.Fired = true;
            cue.FireAt = null;
            Fire((uid, cue));
        }

        TryArm(now);
    }

    /// <summary>
    /// Arms any cue whose sub-goal the player has just reached, and pulls an armed one onto the
    /// coach's line once she reaches it. Polled, because only one system may hold a directed
    /// subscription to <c>TutorialParticipantProgressChangedEvent</c> and the guide already does.
    /// </summary>
    private void TryArm(TimeSpan now)
    {
        var pending = EntityQueryEnumerator<TutorialCueComponent, TransformComponent>();
        while (pending.MoveNext(out var uid, out var cue, out var xform))
        {
            if (cue.Fired)
                continue;

            if (cue.FireAt == null)
            {
                Arm(now, (uid, cue), xform);
                continue;
            }

            TryTakeTheBeatFromTheCoach(now, (uid, cue));
        }
    }

    private void Arm(TimeSpan now, Entity<TutorialCueComponent> cue, TransformComponent xform)
    {
        var players = EntityQueryEnumerator<TutorialParticipantComponent, TransformComponent>();
        while (players.MoveNext(out var player, out var part, out var playerXform))
        {
            // Grid, not map: one player must not set off another player's copy of the facility.
            if (playerXform.GridUid != xform.GridUid)
                continue;

            if (!_tutorial.TryGetCurrentSubGoal(player, part, out var sub) || sub.Id != cue.Comp.SubGoalId)
                continue;

            cue.Comp.FireAt = now + cue.Comp.Delay;
            cue.Comp.ArmedBy = player;
            break;
        }
    }

    /// <summary>
    /// Moves an armed cue onto the line it was written for, and keeps the backstop out of the way
    /// until she gets there.
    /// </summary>
    private void TryTakeTheBeatFromTheCoach(TimeSpan now, Entity<TutorialCueComponent> cue)
    {
        if (cue.Comp.CuedOnLine || cue.Comp.AfterLine is not { } afterLine)
            return;

        if (cue.Comp.ArmedBy is not { } player || TerminatingOrDeleted(player))
            return;

        if (!_trainer.TryGetLinesSpoken(player, cue.Comp.SubGoalId, out var spoken))
            return;

        if (spoken < afterLine)
        {
            cue.Comp.FireAt = now + cue.Comp.Delay;
            return;
        }

        cue.Comp.CuedOnLine = true;

        var onCue = now + cue.Comp.LineDelay;
        if (onCue < cue.Comp.FireAt)
            cue.Comp.FireAt = onCue;
    }

    private void Fire(Entity<TutorialCueComponent> cue)
    {
        switch (cue.Comp.Effect)
        {
            case TutorialCueEffect.LightsOff:
                SetLightsInRange(cue, false);
                break;
            case TutorialCueEffect.LightsOn:
                SetLightsInRange(cue, true);
                break;
            case TutorialCueEffect.Breach:
                Breach(cue);
                break;
            case TutorialCueEffect.Stage:
                PlayEffects(cue);
                break;
            case TutorialCueEffect.Speak:
                Speak(cue);
                break;
            case TutorialCueEffect.Press:
                Press(cue);
                break;
            case TutorialCueEffect.Detain:
                Detain(cue);
                break;
            case TutorialCueEffect.Project:
                Project(cue);
                break;
        }
    }

    /// <summary>
    /// Darkens or relights every fixture in range on this grid. Both component types, because
    /// fixtures like <c>AlwaysPoweredWallLight</c> carry a bare <c>PointLight</c> and no
    /// <c>PoweredLight</c> to switch.
    /// </summary>
    private void SetLightsInRange(Entity<TutorialCueComponent> cue, bool state)
    {
        PlayEffects(cue);

        var xform = Transform(cue);
        var origin = _transform.GetWorldPosition(xform);
        var radiusSquared = cue.Comp.Radius * cue.Comp.Radius;

        var powered = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (powered.MoveNext(out var uid, out var light, out var lightXform))
        {
            if (!InRange(lightXform, xform.GridUid, origin, radiusSquared))
                continue;

            _lights.SetState(uid, state, light);
        }

        var points = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (points.MoveNext(out var uid, out var point, out var lightXform))
        {
            if (!InRange(lightXform, xform.GridUid, origin, radiusSquared))
                continue;

            _pointLight.SetEnabled(uid, state, point);
        }
    }

    private bool InRange(TransformComponent lightXform, EntityUid? gridUid, Vector2 origin, float radiusSquared)
    {
        if (lightXform.GridUid != gridUid)
            return false;

        return Vector2.DistanceSquared(origin, _transform.GetWorldPosition(lightXform)) <= radiusSquared;
    }

    /// <summary>
    /// Sets off a real charge where the cue sits, so the hull is destroyed by the explosion system
    /// rather than deleted out from under it. <c>canCreateVacuum: false</c> keeps the floor intact,
    /// so the room vents through the hole where the window was and not through a pit of its own.
    /// </summary>
    private void Breach(Entity<TutorialCueComponent> cue)
    {
        PlayEffects(cue);

        if (cue.Comp.ArmedBy is { } player && !TerminatingOrDeleted(player))
            _recoil.KickCamera(player, BreachKick);

        _explosions.QueueExplosion(
            cue.Owner,
            cue.Comp.ExplosionType,
            cue.Comp.TotalIntensity,
            cue.Comp.IntensitySlope,
            cue.Comp.MaxIntensity,
            canCreateVacuum: false);

        QueueDel(cue);
    }

    /// <summary>
    /// Has a tagged bystander say one line in their own voice. Routed through the coach's speak
    /// helper so it lands as an ordinary speech bubble, not a system message: the player should
    /// read it as somebody talking, because somebody is.
    /// </summary>
    private void Speak(Entity<TutorialCueComponent> cue)
    {
        PlayEffects(cue);

        if (cue.Comp.Line is not { } line)
            return;

        if (!TryFindTarget(cue, out var speaker))
            return;

        var listener = cue.Comp.ArmedBy ?? speaker;
        _trainer.SpeakAsCoach(speaker, listener, cue.Comp.SubGoalId, Loc.GetString(line), null);
    }

    /// <summary>
    /// Puts a tagged entity on the floor in restraints.
    /// </summary>
    /// <remarks>
    /// Cuffs go on through <see cref="SharedCuffableSystem.TryAddNewCuffs"/> rather than
    /// <c>TryCuffing</c>, which runs a do-after that a scripted beat would have to wait out and
    /// that anything walking past could interrupt.
    /// </remarks>
    /// <summary>
    /// Throws a tagged switch the way a person in the room would. Goes through the device link
    /// rather than raising an interaction, because there is nobody standing at the button: the
    /// point is the shutters coming down, not who reached for it.
    /// </summary>
    private void Press(Entity<TutorialCueComponent> cue)
    {
        PlayEffects(cue);

        // By tag when one is given, otherwise the nearest switch to the cue. Asking a mapper to
        // swap a button they have already placed and linked for an identical one with a tag on it
        // is a step that gets forgotten, and fails silently when it does.
        if (!TryFindTarget(cue, out var target) && !TryFindNearestSwitch(cue, out target))
            return;

        // Through the switch's own activate handler rather than poking the device link, so the
        // click sound, the lock check and the port bookkeeping are the ones the game already has.
        // Complex because that is what the handler asks for; the user is nobody the player sees.
        var ev = new ActivateInWorldEvent(cue.Owner, target, true);
        RaiseLocalEvent(target, ev, true);
    }

    /// <summary>
    /// Turns the cue's bystander to look at whoever the effect is happening to.
    /// </summary>
    private void FaceTarget(Entity<TutorialCueComponent> cue, EntityUid target)
    {
        if (string.IsNullOrEmpty(cue.Comp.FaceTag))
            return;

        var tag = (ProtoId<TagPrototype>) cue.Comp.FaceTag;
        var cueXform = Transform(cue);
        var coords = Transform(target).Coordinates;

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid != cueXform.GridUid || !_tags.HasTag(uid, tag))
                continue;

            _rotate.TryFaceCoordinates(uid, _transform.ToMapCoordinates(coords).Position, xform);
        }
    }

    /// <summary>Nearest signal switch to the cue, on its own grid and within a few tiles.</summary>
    private bool TryFindNearestSwitch(Entity<TutorialCueComponent> cue, out EntityUid target)
    {
        const float range = 6f;

        target = EntityUid.Invalid;

        var cueXform = Transform(cue);
        var origin = _transform.GetWorldPosition(cueXform);
        var best = range * range;

        var query = EntityQueryEnumerator<SignalSwitchComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != cueXform.GridUid)
                continue;

            var distance = Vector2.DistanceSquared(origin, _transform.GetWorldPosition(xform));
            if (distance >= best)
                continue;

            best = distance;
            target = uid;
        }

        return target != EntityUid.Invalid;
    }

    private void Detain(Entity<TutorialCueComponent> cue)
    {
        PlayEffects(cue);

        if (!TryFindTarget(cue, out var target))
            return;

        FaceTarget(cue, target);

        _stun.TryUpdateParalyzeDuration(target, cue.Comp.StunDuration);

        var cuffs = Spawn(cue.Comp.Handcuffs, Transform(target).Coordinates);
        if (!_cuffable.TryAddNewCuffs(target, target, cuffs))
            QueueDel(cuffs);
    }

    /// <summary>
    /// Brings the holopad this cue is on to life and puts the hologram on it.
    /// </summary>
    /// <remarks>
    /// The cue lives on the pad rather than beside it, so <see cref="PlayEffects"/> already spawns
    /// on the right tile and there is nothing for a mapper to line up. Same three switches
    /// <c>TutorialHoloMentorSystem</c> throws when a projecting coach arrives, so a pad lit this
    /// way is the pad the other curricula light.
    /// </remarks>
    private void Project(Entity<TutorialCueComponent> cue)
    {
        var projection = PlayEffects(cue);

        // Handed to the pad so that darkening the pad at the end of the session takes her with it.
        if (TryComp<TutorialHoloPointComponent>(cue, out var point))
            point.Projection = projection;

        _appearance.SetData(cue, TutorialHoloPointVisuals.Active, true);
        _pointLight.SetEnabled(cue, true);
        _ambient.SetAmbience(cue, true);
    }

    /// <summary>
    /// Nearest entity carrying <see cref="TutorialCueComponent.TargetTag"/> on the cue's grid.
    /// Grid rather than map, for the same reason arming is: one player's set piece must not reach
    /// into another player's copy of the facility.
    /// </summary>
    private bool TryFindTarget(Entity<TutorialCueComponent> cue, out EntityUid target)
        => TryFindTagged(cue, cue.Comp.TargetTag, out target);

    /// <inheritdoc cref="TryFindTarget"/>
    private bool TryFindTagged(Entity<TutorialCueComponent> cue, string? wanted, out EntityUid target)
    {
        target = EntityUid.Invalid;

        if (string.IsNullOrEmpty(wanted))
            return false;

        var cueXform = Transform(cue);
        var origin = _transform.GetWorldPosition(cueXform);
        var tag = (ProtoId<TagPrototype>) wanted;

        var best = float.MaxValue;
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid != cueXform.GridUid || !_tags.HasTag(uid, tag))
                continue;

            var distance = Vector2.DistanceSquared(origin, _transform.GetWorldPosition(xform));
            if (distance >= best)
                continue;

            best = distance;
            target = uid;
        }

        return target != EntityUid.Invalid;
    }

    /// <summary>
    /// Plays the cue's sound and stages its entity, returning whatever it put in the world.
    /// </summary>
    private EntityUid? PlayEffects(Entity<TutorialCueComponent> cue)
    {
        if (cue.Comp.Sound is { } sound)
            _audio.PlayPvs(sound, cue);

        if (cue.Comp.Spawn is not { } spawn)
            return null;

        var spawned = Spawn(spawn, Transform(cue).Coordinates);
        SendAfterTarget(cue, spawned);
        return spawned;
    }

    /// <summary>
    /// Points a freshly staged NPC at whoever they turned up for, and keeps them pointed at them:
    /// the target is coordinates <i>on</i> that entity, so the walk follows them if they move.
    /// </summary>
    private void SendAfterTarget(Entity<TutorialCueComponent> cue, EntityUid spawned)
    {
        if (string.IsNullOrEmpty(cue.Comp.SpawnFollowTag))
            return;

        if (!TryFindTagged(cue, cue.Comp.SpawnFollowTag, out var target))
            return;

        var destination = new EntityCoordinates(target, Vector2.Zero);

        if (TryComp<HTNComponent>(spawned, out var htn))
        {
            _npc.SetBlackboard(spawned, NPCBlackboard.FollowTarget, destination, htn);
            _htn.Replan(htn);
            return;
        }

        _npc.SetBlackboard(spawned, NPCBlackboard.FollowTarget, destination);
    }
}
