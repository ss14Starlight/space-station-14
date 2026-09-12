using System.Numerics;
using Content.Shared._Functional.TutorialServer;
using Content.Server.Power.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Disposal.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Electrocution;
using Content.Shared.Tag;
using Content.Shared.VendingMachines;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Sensors for the passenger curriculum: doors that say no, shocks, bolts, construction, disposal
/// travel and vending machines. Several of these complete on a <i>failure</i>, because being
/// refused by the station is the thing this curriculum teaches.
/// </summary>
public sealed partial class TutorialGoalSensorSystem
{
    [Dependency] private AccessReaderSystem _access = default!;

    /// <summary>Participants who have taken a shock since that drill became current.</summary>
    private readonly HashSet<EntityUid> _shocked = new();

    /// <summary>Participants who have opened the construction menu.</summary>
    private readonly HashSet<EntityUid> _openedConstruction = new();

    /// <summary>Participants already told that the thing they are working on has no power.</summary>
    private readonly HashSet<EntityUid> _unpoweredWarned = new();

    /// <summary>
    /// Default for <see cref="TutorialStepComplete.EntityAtMarker"/>: a tile and a half, matching
    /// the parked-crate drill, because things land where they land. Beats whose marker sits next to
    /// something that would already match override it with
    /// <see cref="TutorialSubGoalData.MarkerRange"/>.
    /// </summary>
    private const float AtMarkerRange = 1.5f;

    private void InitializeTide()
    {
        SubscribeLocalEvent<TutorialSensorTargetComponent, BeforeDoorOpenedEvent>(OnSensorTargetDoorAttempt);
        SubscribeLocalEvent<TutorialParticipantComponent, ElectrocutedEvent>(OnParticipantElectrocuted);
        SubscribeNetworkEvent<TutorialConstructionMenuOpenedEvent>(OnConstructionMenuOpened);
    }

    /// <summary>Polled passenger sensors: states, so a beat already satisfied on entry passes too.</summary>
    private void UpdateTideSensors(EntityUid uid, TransformComponent xform, TutorialSubGoalData sub)
    {
        switch (sub.Complete)
        {
            case TutorialStepComplete.DoorAccessDenied:
                if (WasDeniedByTaggedDoor(uid, sub.Tag))
                {
                    _deniedTarget.Remove(uid);
                    _tutorial.AdvanceSubGoal(uid);
                }
                break;
            case TutorialStepComplete.PlayerShocked:
                if (_shocked.Remove(uid))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.DoorBoltsRaised:
                if (AreBoltsRaised(xform.MapUid, sub.Tag))
                    _tutorial.AdvanceSubGoal(uid);
                else
                    WarnIfTargetUnpowered(uid, xform.MapUid, sub);
                break;
            case TutorialStepComplete.ConstructionMenuOpened:
                if (_openedConstruction.Remove(uid))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.PlayerInDisposal:
                if (IsInsideTaggedDisposal(uid, sub.Tag))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.VendorContrabandUnlocked:
                if (IsVendorContrabandUnlocked(xform.MapUid, sub.Tag))
                    _tutorial.AdvanceSubGoal(uid);
                else
                    WarnIfTargetUnpowered(uid, xform.MapUid, sub);
                break;
            case TutorialStepComplete.EntityAtMarker:
                if (IsEntityAtMarker(xform, sub))
                    _tutorial.AdvanceSubGoal(uid);
                break;
        }
    }

    /// <summary>
    /// Records a door refusing a participant.
    /// </summary>
    /// <remarks>
    /// Hung off the door's own open attempt rather than off a click, because walking into a locked
    /// door is how most players meet one: <c>SharedDoorSystem.HandleCollide</c> and the activate
    /// path both funnel through <c>CanOpen</c>, which raises this. Never cancels — the door still
    /// has to play its own deny animation, which is the feedback the beat is teaching the player to
    /// recognise. The access check is re-run here on the reader the door would have used, since
    /// <c>Deny</c> itself raises nothing to listen for.
    /// </remarks>
    private void OnSensorTargetDoorAttempt(Entity<TutorialSensorTargetComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (args.User is not { } user || !HasComp<TutorialParticipantComponent>(user))
            return;

        // Bolted doors refuse everyone, access or not, and that is a different lesson with a
        // different fix. Only a refusal the card could have prevented counts here.
        if (TryComp<DoorBoltComponent>(ent, out var bolt) && bolt.BoltsDown)
            return;

        if (_access.IsAllowed(user, ent))
            return;

        _deniedTarget[user] = ent.Owner;
    }

    /// <summary>Door the participant was last refused by, so a tagged drill can check the right one.</summary>
    private readonly Dictionary<EntityUid, EntityUid> _deniedTarget = new();

    private bool WasDeniedByTaggedDoor(EntityUid player, string? tag)
    {
        if (!_deniedTarget.TryGetValue(player, out var door))
            return false;

        if (TerminatingOrDeleted(door))
        {
            _deniedTarget.Remove(player);
            return false;
        }

        // No tag means any refusal counts, which is what the opening beat wants.
        return string.IsNullOrEmpty(tag) || _tags.HasTag(door, (ProtoId<TagPrototype>) tag);
    }

    private void OnParticipantElectrocuted(Entity<TutorialParticipantComponent> ent, ref ElectrocutedEvent args)
    {
        _shocked.Add(ent.Owner);
    }

    /// <summary>
    /// The construction menu is client-side, so the client reports it. Attributed to the session's
    /// attached entity rather than anything in the message, so one player cannot advance another.
    /// </summary>
    private void OnConstructionMenuOpened(TutorialConstructionMenuOpenedEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!HasComp<TutorialParticipantComponent>(player))
            return;

        _openedConstruction.Add(player);
    }

    /// <summary>
    /// True when a tagged door's bolts are up. State, not the pulse that raised them, so a player
    /// who gets there some other way has still solved it. The door has to spawn bolted for this to
    /// mean anything — an unbolted one passes on the frame the beat starts.
    /// </summary>
    private bool AreBoltsRaised(EntityUid? mapUid, string? tag)
    {
        if (mapUid == null || string.IsNullOrEmpty(tag))
            return false;

        var tagId = (ProtoId<TagPrototype>) tag;
        var query = EntityQueryEnumerator<DoorBoltComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bolt, out var xform))
        {
            if (xform.MapUid != mapUid || !_tags.HasTag(uid, tagId))
                continue;

            if (!bolt.BoltsDown)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the participant is inside a tagged disposal unit. Read off the container rather
    /// than the unit's own state, so it holds for the moment between climbing in and flushing.
    /// </summary>
    private bool IsInsideTaggedDisposal(EntityUid player, string? tag)
    {
        if (!_containers.TryGetContainingContainer((player, null, null), out var container))
            return false;

        var unit = container.Owner;
        if (!HasComp<DisposalUnitComponent>(unit))
            return false;

        return string.IsNullOrEmpty(tag) || _tags.HasTag(unit, (ProtoId<TagPrototype>) tag);
    }

    private bool IsVendorContrabandUnlocked(EntityUid? mapUid, string? tag)
    {
        if (mapUid == null)
            return false;

        var query = EntityQueryEnumerator<VendingMachineComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vendor, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            if (!string.IsNullOrEmpty(tag) && !_tags.HasTag(uid, (ProtoId<TagPrototype>) tag))
                continue;

            if (vendor.Contraband)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when something matching the spec is resting at the marker. "Resting" is the point:
    /// anything still in a hand or a bag does not count, so the drill teaches putting it down.
    /// </summary>
    private bool IsEntityAtMarker(TransformComponent playerXform, TutorialSubGoalData sub)
    {
        if (string.IsNullOrEmpty(sub.Marker) || !HasItemSpec(sub))
            return false;

        var mapId = playerXform.MapID;
        if (mapId == MapId.Nullspace)
            return false;

        if (!TryGetMarkerCoords(mapId, sub.Marker, out var markerCoords))
            return false;

        var markerPos = _transform.ToMapCoordinates(markerCoords).Position;
        var range = sub.MarkerRange ?? AtMarkerRange;

        var found = 0;
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            // Contained means held, worn or bagged — none of which is "on the counter".
            if (_containers.IsEntityInContainer(uid))
                continue;

            if (!MatchesItemSpec(uid, sub))
                continue;

            var pos = _transform.GetWorldPosition(xform);
            if (Vector2.Distance(pos, markerPos) > range)
                continue;

            if (++found >= sub.MinCount)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Says so when a drill has been made unwinnable by taking the target's power off.
    /// </summary>
    /// <remarks>
    /// Two beats can be lost this way and neither says anything when it happens. Bolts are motors,
    /// so <c>TrySetBoltDown</c> refuses outright on a dark door: the wire is intact, the panel light
    /// still reads bolted, and pulsing it does nothing at all. A vending machine whose power wire
    /// has been cut or pulsed likewise will not give up its stock. In both cases the coach is the
    /// only thing that can tell the player, so he does, once, when it goes dark, and again if they
    /// mend it and take it off a second time.
    /// </remarks>
    private void WarnIfTargetUnpowered(EntityUid player, EntityUid? mapUid, TutorialSubGoalData sub)
    {
        if (sub.RetryLine is not { } line || string.IsNullOrEmpty(sub.Tag))
            return;

        // PowerDisabled only, not IsTargetPowerDisabled: that one also reports a receiver whose
        // Powered flag has not caught up yet, which is true for a tick or two after a wire is
        // mended. Warning them off the thing they just did correctly is worse than saying nothing.
        if (!IsTargetPowerCut(mapUid, sub.Tag))
        {
            _unpoweredWarned.Remove(player);
            return;
        }

        if (!_unpoweredWarned.Add(player))
            return;

        if (!_tutorial.TryGetSession(player, out var session))
            return;

        var mentor = session.MentorUid;
        if (mentor != EntityUid.Invalid && !TerminatingOrDeleted(mentor))
            _trainer.TrySpeakInterjection(mentor, player, line);
    }

    /// <summary>
    /// True when a tagged target has been deliberately switched off: a cut or pulsed power wire,
    /// rather than a receiver that is merely between ticks.
    /// </summary>
    private bool IsTargetPowerCut(EntityUid? mapUid, string? tag)
    {
        if (mapUid == null || string.IsNullOrEmpty(tag))
            return false;

        var tagId = (ProtoId<TagPrototype>) tag;
        var query = EntityQueryEnumerator<ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var power, out var xform))
        {
            if (xform.MapUid != mapUid || !_tags.HasTag(uid, tagId))
                continue;

            if (power.PowerDisabled)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Clears the per-participant latches. Called when a session ends, so a second run of the
    /// curriculum does not start with a shock or a refusal already banked.
    /// </summary>
    public void ClearTideLatches(EntityUid player)
    {
        _deniedTarget.Remove(player);
        _shocked.Remove(player);
        _openedConstruction.Remove(player);
        _unpoweredWarned.Remove(player);
    }
}
