using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.TransitTube;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.DeviceLinking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Damage.Systems;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.TransitTube;

public sealed partial class TransitTubeSystem : SharedTransitTubeSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DisposalTubeSystem _disposalTube = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDisposalHolderSystem _holder = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    private static readonly TimeSpan _atmosInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan _collisionCooldown = TimeSpan.FromSeconds(1);

    private readonly HashSet<Entity<TransitPodComponent>> _nearbyPods = [];


    [SubscribeLocalEvent]
    private void OnPodMapInit(Entity<TransitPodComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DisposalHolderComponent>(ent, out var holder))
            return;

        var xform = Transform(ent);
        ent.Comp.TravelDirection = xform.LocalRotation.GetDir();
        ent.Comp.BaseSpeed = holder.TraversalSpeed;
        _holder.SetHalted((ent, holder), true);

        // A new pod takes the air around it.
        var air = holder.Air;
        if (_atmos.GetTileMixture((ent, xform)) is { } env && env.TotalMoles > 0)
        {
            air.Clear();
            _atmos.Merge(air, env.RemoveVolume(air.Volume));
        }

        if (TryGetTube(xform, out var tube)
            && TryComp<TransitTubeStationComponent>(tube, out var station)
            && station.DockedPod == null)
        {
            Dock(ent, (ent, holder), (tube, station), instant: true);
            return;
        }

        SetState(ent, TransitPodState.Stalled);
    }

    [SubscribeLocalEvent]
    private void OnPodExitAttempt(Entity<TransitPodComponent> ent, ref DisposalHolderExitAttemptEvent args)
    {
        // Pods never break out of the tube: they dock, turn around or stop.
        args.Cancelled = true;

        if (!TryComp<DisposalHolderComponent>(ent, out var holder))
            return;

        var target = ent.Comp.DockTarget;
        ent.Comp.DockTarget = null;

        if (ent.Comp.State != TransitPodState.Moving)
        {
            _holder.SetHalted((ent, holder), true);
            return;
        }

        if (target is { } stationUid
            && TryComp<TransitTubeStationComponent>(stationUid, out var station)
            && station.DockedPod == null)
        {
            Dock(ent, (ent, holder), (stationUid, station));
            return;
        }

        if (ent.Comp.Boosted)
        {
            // The tube ahead broke under the hits: the pod bursts out of the line.
            if (!Exists(holder.NextTube))
            {
                Derail(ent, (ent, holder));
                return;
            }

            // Too fast to stop: slam into the end and bounce back, every time.
            Impact(ent, (ent, holder));
            if (Bounce((ent, holder)))
                return;
        }

        // Dead end or a busy station: go back once, stop if the way back is a dead end too.
        if (!ent.Comp.Bounced && Bounce((ent, holder)))
        {
            ent.Comp.Bounced = true;
            return;
        }

        Stall(ent, (ent, holder));
    }

    [SubscribeLocalEvent]
    private void OnPodTerminating(Entity<TransitPodComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Station is { } stationUid && TryComp<TransitTubeStationComponent>(stationUid, out var station))
            Undock((stationUid, station));

        EjectAll(ent, Transform(ent).Coordinates);
    }

    [SubscribeLocalEvent]
    private void OnStationAnchorChanged(Entity<TransitTubeStationComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            ReleaseDockedPod(ent);
    }

    [SubscribeLocalEvent]
    private void OnStationTerminating(Entity<TransitTubeStationComponent> ent, ref EntityTerminatingEvent args)
        => ReleaseDockedPod(ent);

    private void ReleaseDockedPod(Entity<TransitTubeStationComponent> station)
    {
        if (station.Comp.DockedPod is not { } podUid || !TryComp<TransitPodComponent>(podUid, out var pod))
            return;

        Undock(station);
        pod.Station = null;
        SetState((podUid, pod), TransitPodState.Stalled);
        EjectAll((podUid, pod), Transform(podUid).Coordinates);
    }
}
