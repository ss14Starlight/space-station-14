using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.Disposal.Unit;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared._Starlight.TransitTube;

public abstract partial class SharedTransitTubeSystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnStationActivate(Entity<TransitTubeStationComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = ActivateStation(ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnPodActivate(Entity<TransitPodComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || IsPassenger(ent, args.User))
            return;

        // A docked pod is used like its station: clicking it gets you in.
        if (ent.Comp.Station is { } stationUid && TryComp<TransitTubeStationComponent>(stationUid, out var station))
        {
            args.Handled = ActivateStation((stationUid, station), args.User);
            return;
        }

        args.Handled = TryPushStalled(ent, args.User);
    }

    private bool ActivateStation(Entity<TransitTubeStationComponent> station, EntityUid user)
    {
        if (station.Comp.DockedPod is { } podUid
            && TryComp<TransitPodComponent>(podUid, out var pod)
            && !pod.Cargo)
            return TryBoard(station, user, user, predicted: true);

        return TryStationAction(station, user);
    }

    protected virtual bool TryStationAction(Entity<TransitTubeStationComponent> station, EntityUid user)
        => false;

    protected virtual bool TryPushStalled(Entity<TransitPodComponent> pod, EntityUid user)
        => false;

    [SubscribeLocalEvent]
    private void OnStationVerbs(Entity<TransitTubeStationComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Depot)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("transit-station-verb-destination"),
            // Above climbing onto the tube, so alt-click opens the list.
            Priority = 2,
            Act = () =>
            {
                OnDestinationsOpening(ent);
                _ui.OpenUi(ent.Owner, TransitStationUiKey.Key, user, predicted: true);
            },
        });
    }

    protected virtual void OnDestinationsOpening(Entity<TransitTubeStationComponent> station)
    {
    }

    protected bool TryBoard(Entity<TransitTubeStationComponent> station, EntityUid passenger, EntityUid user, bool predicted = false)
    {
        if (station.Comp.DockedPod is { } podUid
            && TryComp<TransitPodComponent>(podUid, out var pod)
            && !CanCarry(pod, passenger))
        {
            if (passenger == user || HasComp<MobStateComponent>(passenger))
                PopupUser(Loc.GetString(pod.Cargo ? "transit-pod-cargo-only" : "transit-pod-passengers-only"), station, user, predicted);
            return passenger == user;
        }

        if (station.Comp.DockedPod == null && !HasComp<MobStateComponent>(passenger))
            return false;

        if (!CanBoard(station, user, popup: true, predicted) || !CheckPlatformSide(station, passenger, user, predicted))
            return true;

        var self = passenger == user;
        var delay = self ? station.Comp.BoardDelay : station.Comp.DraggedBoardDelay;

        // Only the server tells whoever is being put in; the user's client has nobody to show it to.
        if (!self)
            _popup.PopupEntity(Loc.GetString("transit-pod-being-boarded", ("user", Identity.Entity(user, EntityManager))), passenger, passenger, PopupType.Large);

        var doAfter = new DoAfterArgs(EntityManager, user, delay, new TransitPodBoardDoAfterEvent(), station, target: passenger, used: station)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    protected bool CanBoard(Entity<TransitTubeStationComponent> station, EntityUid user, bool popup, bool predicted = false)
    {
        string? reason = null;

        if (station.Comp.DockedPod is not { } podUid || !TryComp<TransitPodComponent>(podUid, out var pod))
            reason = "transit-pod-none";
        else if (station.Comp.Depot)
            reason = "transit-station-depot";
        else if (!_access.IsAllowed(user, station))
            reason = "transit-station-access-denied";
        else if (pod.State != TransitPodState.Open)
            reason = "transit-pod-not-ready";
        else if (GetPassengers((podUid, pod)).Count() >= pod.MaxPassengers)
            reason = "transit-pod-full";

        if (reason != null && popup)
            PopupUser(Loc.GetString(reason), station, user, predicted);

        return reason == null;
    }

    protected bool OnPlatformSide(Entity<TransitTubeStationComponent> station, EntityUid uid)
    {
        // Held things sit at their carrier's position, so this covers them too.
        var stationXform = Transform(station);
        var offset = _xform.GetWorldPosition(uid) - _xform.GetWorldPosition(stationXform);
        var side = _xform.GetWorldRotation(stationXform).RotateVec(station.Comp.BoardingSide.ToVec());
        return System.Numerics.Vector2.Dot(offset, side) > 0.1f;
    }

    protected bool CheckPlatformSide(Entity<TransitTubeStationComponent> station, EntityUid passenger, EntityUid user, bool predicted = false)
    {
        if (OnPlatformSide(station, passenger))
            return true;

        PopupUser(Loc.GetString("transit-station-wrong-side"), station, user, predicted);
        return false;
    }

    private void PopupUser(string message, EntityUid at, EntityUid user, bool predicted)
    {
        if (predicted)
            _popup.PopupClient(message, at, user);
        else
            _popup.PopupEntity(message, at, user);
    }

    protected IEnumerable<EntityUid> GetPassengers(Entity<TransitPodComponent> pod)
    {
        if (!TryComp<DisposalHolderComponent>(pod, out var holder) || holder.Container == null)
            return [];

        return holder.Container.ContainedEntities;
    }

    protected bool IsPassenger(Entity<TransitPodComponent> pod, EntityUid uid)
        => GetPassengers(pod).Contains(uid);
}
