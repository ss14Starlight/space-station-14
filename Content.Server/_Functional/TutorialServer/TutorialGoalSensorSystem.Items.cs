using System.Linq;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Body.Components;
using Content.Shared.Disposal.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Sensors for the items-and-survival curriculum: hands, the four ways to use a thing, storage,
/// and breathing.
/// </summary>
public sealed partial class TutorialGoalSensorSystem
{
    /// <summary>Active hand each participant had when a swap-hands drill began.</summary>
    private readonly Dictionary<EntityUid, string> _handBaseline = new();

    /// <summary>A tile and a half: the crate snaps to tiles, the player stops anywhere.</summary>
    private const float ParkedAtMarkerRange = 1.5f;

    private void InitializeItems()
    {
        SubscribeLocalEvent<TutorialSensorTargetComponent, ExaminedEvent>(OnSensorTargetExamined);
        SubscribeLocalEvent<TutorialSensorTargetComponent, ActivateInWorldEvent>(OnSensorTargetActivated);
        SubscribeLocalEvent<ItemComponent, ThrownEvent>(OnItemThrown);
    }

    /// <summary>Polled item sensors: all states, so they also pass if already satisfied on entry.</summary>
    private void UpdateItemSensors(EntityUid uid, TransformComponent xform, TutorialSubGoalData sub)
    {
        switch (sub.Complete)
        {
            case TutorialStepComplete.PlayerSwappedHands:
                if (HasSwappedHands(uid))
                {
                    _handBaseline.Remove(uid);
                    _tutorial.AdvanceSubGoal(uid);
                }
                break;
            case TutorialStepComplete.StorageOpened:
                if (IsMatchingStorageOpen(uid, sub))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.DisposalEngaged:
                if (IsDisposalEngaged(xform.MapUid, sub.Tag))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.BreathToolEquipped:
                if (TryComp<InternalsComponent>(uid, out var breathing) && breathing.BreathTools.Count > 0)
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.InternalsOn:
                if (TryComp<InternalsComponent>(uid, out var internals) && internals.GasTankEntity != null)
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.ActiveHandItem:
                if (IsActiveHandMatch(uid, sub))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.TargetAnchored:
                if (IsTargetAnchored(xform.MapUid, sub))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.TargetAbsent:
                if (IsTargetAbsent(xform.MapUid, sub.Tag))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.HandsEmpty:
                if (AreHandsEmpty(uid))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.ActiveHandEmpty:
                if (_hands.ActiveHandIsEmpty(uid))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.TargetParkedAtMarker:
                if (IsTargetParkedAtMarker(xform, sub))
                    _tutorial.AdvanceSubGoal(uid);
                break;
            case TutorialStepComplete.InternalsOff:
                if (!TryComp<InternalsComponent>(uid, out var off) || off.GasTankEntity == null)
                    _tutorial.AdvanceSubGoal(uid);
                break;
        }
    }

    /// <summary>
    /// True when a tagged entity sits on the marker and nothing is pulling it. The second half is
    /// the point: letting go is its own control, and ReachMarker would never teach it.
    /// </summary>
    private bool IsTargetParkedAtMarker(TransformComponent playerXform, TutorialSubGoalData sub)
    {
        if (string.IsNullOrEmpty(sub.Tag) || string.IsNullOrEmpty(sub.Marker))
            return false;

        var mapId = playerXform.MapID;
        if (mapId == MapId.Nullspace)
            return false;

        if (!TryGetMarkerCoords(mapId, sub.Marker, out var markerCoords))
            return false;

        var markerPos = _transform.ToMapCoordinates(markerCoords).Position;
        var tag = (ProtoId<TagPrototype>) sub.Tag;

        var query = EntityQueryEnumerator<PullableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pullable, out var xform))
        {
            if (xform.MapID != mapId || !_tags.HasTag(uid, tag))
                continue;

            if (pullable.Puller != null)
                continue;

            if ((_transform.GetWorldPosition(xform) - markerPos).Length() <= ParkedAtMarkerRange)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the item in the active hand matches the sub-goal's spec. The active hand is the
    /// one <c>InteractUsing</c> reads, so this is "hold the right tool" where HoldItem is not.
    /// </summary>
    private bool IsActiveHandMatch(EntityUid uid, TutorialSubGoalData sub)
    {
        if (!TryComp<HandsComponent>(uid, out var hands) ||
            hands.ActiveHandId is not { } active)
            return false;

        if (!_hands.TryGetHeldItem((uid, hands), active, out var held))
            return false;

        if (!string.IsNullOrEmpty(sub.Tag) && !_tags.HasTag(held.Value, (ProtoId<TagPrototype>) sub.Tag))
            return false;

        return HasItemSpec(sub) ? MatchesItemSpec(held.Value, sub) : !string.IsNullOrEmpty(sub.Tag);
    }

    /// <summary>
    /// True when a tagged entity on the player's map has reached the wanted anchor state. The
    /// state, not the click: unbolting is a do-after and a cancelled one leaves it anchored.
    /// </summary>
    private bool IsTargetAnchored(EntityUid? mapUid, TutorialSubGoalData sub)
    {
        if (mapUid == null || string.IsNullOrEmpty(sub.Tag))
            return false;

        var tag = (ProtoId<TagPrototype>) sub.Tag;
        var found = false;
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.MapUid != mapUid || TerminatingOrDeleted(uid) || !_tags.HasTag(uid, tag))
                continue;

            found = true;
            if (xform.Anchored == sub.Anchored)
                return true;
        }

        // Gone counts as unbolted, so taking the girder apart early cannot strand the player.
        // One direction only: waiting for something to be bolted down is not met by its absence.
        return !found && !sub.Anchored;
    }

    /// <summary>True once the drill's target is gone: deconstructing it deletes it.</summary>
    private bool IsTargetAbsent(EntityUid? mapUid, string? tag)
    {
        if (mapUid == null || string.IsNullOrEmpty(tag))
            return false;

        var tagId = (ProtoId<TagPrototype>) tag;
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.MapUid == mapUid && !TerminatingOrDeleted(uid) && _tags.HasTag(uid, tagId))
                return false;
        }

        return true;
    }

    private bool AreHandsEmpty(EntityUid uid)
    {
        return TryComp<HandsComponent>(uid, out var hands) && _hands.EnumerateHeld((uid, hands)).Count() == 0;
    }

    /// <summary>
    /// True once the active hand differs from the one held when the drill started. Polled against
    /// a baseline because <c>HandSelectedEvent</c> is raised on the item, and an empty hand has none.
    /// </summary>
    private bool HasSwappedHands(EntityUid uid)
    {
        if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHandId is not { } active)
            return false;

        if (!_handBaseline.TryGetValue(uid, out var started))
        {
            _handBaseline[uid] = active;
            return false;
        }

        return started != active;
    }

    /// <summary>
    /// True when the storage the sub-goal named is open in front of this player. Polled, not hooked
    /// to <c>BoundUIOpenedEvent</c>, so an already-open bag is not asked to be closed and reopened.
    /// </summary>
    private bool IsMatchingStorageOpen(EntityUid uid, TutorialSubGoalData sub)
    {
        // A slot lets "your bag" mean whichever of backpack, satchel or duffel they chose.
        if (!string.IsNullOrEmpty(sub.Slot))
        {
            return _inventory.TryGetSlotEntity(uid, sub.Slot, out var equipped) &&
                   IsStorageOpenFor(equipped.Value, uid);
        }

        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (!MatchesStorageSpec(item, sub))
                continue;

            if (IsStorageOpenFor(item, uid))
                return true;
        }

        // Storage inside worn storage: the survival box is still in the bag when it is opened.
        var enumerator = _inventory.GetSlotEnumerator(uid);
        while (enumerator.NextItem(out var worn))
        {
            if (!TryComp<StorageComponent>(worn, out var outer))
                continue;

            foreach (var stored in outer.Container.ContainedEntities)
            {
                if (MatchesStorageSpec(stored, sub) && IsStorageOpenFor(stored, uid))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Matches by tag, prototype or component; naming none of them matches nothing.</summary>
    private bool MatchesStorageSpec(EntityUid item, TutorialSubGoalData sub)
    {
        if (!string.IsNullOrEmpty(sub.Tag))
            return _tags.HasTag(item, (ProtoId<TagPrototype>) sub.Tag);

        return HasItemSpec(sub) && MatchesItemSpec(item, sub);
    }

    private bool IsStorageOpenFor(EntityUid storage, EntityUid actor)
    {
        return HasComp<StorageComponent>(storage) &&
               _ui.IsUiOpen(storage, StorageComponent.StorageUiKey.Key, actor);
    }

    /// <summary>
    /// True when a tagged disposal unit on this map is engaged. Flush is its top-priority alt verb,
    /// which is what alt-interact runs, so an engaged unit proves the player used that key.
    /// </summary>
    private bool IsDisposalEngaged(EntityUid? mapUid, string? tag)
    {
        if (mapUid == null || string.IsNullOrEmpty(tag))
            return false;

        var tagId = (ProtoId<TagPrototype>) tag;
        var query = EntityQueryEnumerator<DisposalUnitComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var unit, out var xform))
        {
            if (xform.MapUid != mapUid || !_tags.HasTag(uid, tagId))
                continue;

            if (unit.Engaged)
                return true;
        }

        return false;
    }

    private void OnSensorTargetExamined(Entity<TutorialSensorTargetComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<TutorialParticipantComponent>(args.Examiner, out var part))
            return;

        if (!_tutorial.TryGetCurrentSubGoal(args.Examiner, part, out var sub))
            return;

        if (sub.Complete != TutorialStepComplete.ExamineTag)
            return;

        // Named by tag or by prototype: a drill about the card the player already spawned with has
        // no tag to hang off, because that card is the station's and not the curriculum's.
        if (!HasItemSpec(sub) || !MatchesItemSpec(ent.Owner, sub))
            return;

        _tutorial.AdvanceSubGoal(args.Examiner);
    }

    /// <summary>
    /// The activate key on a world target, and only that. Watched on the target because the
    /// user-side <c>UserActivateInWorldEvent</c> only fires when nothing consumed the keypress,
    /// and an opening locker consumes it. Never sets Handled, or the locker would not open.
    /// </summary>
    private void OnSensorTargetActivated(Entity<TutorialSensorTargetComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!TryComp<TutorialParticipantComponent>(args.User, out var part))
            return;

        if (!_tutorial.TryGetCurrentSubGoal(args.User, part, out var sub))
            return;

        if (sub.Complete != TutorialStepComplete.ActivateInWorldTag)
            return;

        if (string.IsNullOrEmpty(sub.Tag) || !_tags.HasTag(ent.Owner, (ProtoId<TagPrototype>) sub.Tag))
            return;

        _tutorial.AdvanceSubGoal(args.User);
    }

    /// <summary>Fires on the throw itself, hit or miss, so a bad throw cannot strand anyone.</summary>
    private void OnItemThrown(Entity<ItemComponent> item, ref ThrownEvent args)
    {
        if (args.User is not { } user || !TryComp<TutorialParticipantComponent>(user, out var part))
            return;

        if (!_tutorial.TryGetCurrentSubGoal(user, part, out var sub))
            return;

        if (sub.Complete != TutorialStepComplete.ThrewItem)
            return;

        if (!HasItemSpec(sub) || !MatchesItemSpec(item.Owner, sub))
            return;

        _tutorial.AdvanceSubGoal(user);
    }
}
