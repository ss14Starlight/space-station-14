using System.Linq;
using Content.Server.Construction.Completions;
using Content.Server.Popups;
using Content.Shared.VentCrawl.Tube.Components;
using Content.Shared.VentCrawl.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Content.Shared.VentCrawl;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.VentCrawl
{
    public sealed class VentCrawlTubeSystem : EntitySystem
    {
        [Dependency] private readonly SharedVentCrawlableSystem _ventCrawableSystem = default!;
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly SharedMoverController _mover = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<VentCrawlTubeComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<VentCrawlTubeComponent, ComponentRemove>(OnComponentRemove);

            SubscribeLocalEvent<VentCrawlTubeComponent, AnchorStateChangedEvent>(OnAnchorChange);
            SubscribeLocalEvent<VentCrawlTubeComponent, BreakageEventArgs>(OnBreak);
            SubscribeLocalEvent<VentCrawlTubeComponent, ComponentShutdown>(OnShutdown);
            SubscribeLocalEvent<VentCrawlTubeComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<VentCrawlTubeComponent, ConstructionBeforeDeleteEvent>(OnDeconstruct);
            SubscribeLocalEvent<VentCrawlBendComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetBendConnectableDirections);
            SubscribeLocalEvent<VentCrawlEntryComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetEntryConnectableDirections);
            SubscribeLocalEvent<VentCrawlJunctionComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetJunctionConnectableDirections);
            SubscribeLocalEvent<VentCrawlTransitComponent, GetVentCrawlsConnectableDirectionsEvent>(OnGetTransitConnectableDirections);
            SubscribeLocalEvent<VentCrawlEntryComponent, GetVerbsEvent<AlternativeVerb>>(AddClimbedVerb);
            SubscribeLocalEvent<VentCrawlerComponent, EnterVentDoAfterEvent>(OnDoAfterEnterTube);
        }

        private void AddClimbedVerb(EntityUid uid, VentCrawlEntryComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!TryComp<VentCrawlerComponent>(args.User, out var ventCrawlerComponent) || HasComp<BeingVentCrawlComponent>(args.User))
                return;

            var xform = Transform(uid);

            if (!xform.Anchored)
                return;

            AlternativeVerb verb = new()
            {
                Act = () => TryEnter(uid, args.User, ventCrawlerComponent),
                Text = Loc.GetString("comp-climbable-verb-climb")
            };
            args.Verbs.Add(verb);
        }

        private void OnDoAfterEnterTube(EntityUid uid, VentCrawlerComponent component, EnterVentDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
                return;

            TryInsert(args.Args.Target.Value, args.Args.Used.Value);

            args.Handled = true;
        }

        private void TryEnter(EntityUid uid, EntityUid user, VentCrawlerComponent crawler)
        {
            if (TryComp<WeldableComponent>(uid, out var weldableComponent))
            {
                if (weldableComponent.IsWelded)
                {
                    _popup.PopupEntity(Loc.GetString("entity-storage-component-welded-shut-message"), user);
                    return;
                }
            }

            var args = new DoAfterArgs(EntityManager, user, crawler.EnterDelay, new EnterVentDoAfterEvent(), user, uid, user)
            {
                BreakOnMove = true,
                BreakOnDamage = true // STARLIGHT
            };

            _doAfterSystem.TryStartDoAfter(args);
        }

        private void OnComponentInit(EntityUid uid, VentCrawlTubeComponent tube, ComponentInit args)
        {
            tube.Contents = _containerSystem.EnsureContainer<Container>(uid, tube.ContainerId);
        }

        private void OnComponentRemove(EntityUid uid, VentCrawlTubeComponent tube, ComponentRemove args)
        {
            DisconnectTube(uid, tube);
        }

        private void OnShutdown(EntityUid uid, VentCrawlTubeComponent tube, ComponentShutdown args)
        {
            DisconnectTube(uid, tube);
        }

        private void OnGetBendConnectableDirections(EntityUid uid, VentCrawlBendComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
        {
            var direction = Transform(uid).LocalRotation;
            var side = new Angle(MathHelper.DegreesToRadians(direction.Degrees - 90));

            args.Connectable = new[] { direction.GetDir(), side.GetDir() };
        }

        private void OnGetEntryConnectableDirections(EntityUid uid, VentCrawlEntryComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
        {
            args.Connectable = new[] { Transform(uid).LocalRotation.GetDir() };
        }

        private void OnGetJunctionConnectableDirections(EntityUid uid, VentCrawlJunctionComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
        {
            var direction = Transform(uid).LocalRotation;

            args.Connectable = component.Degrees
                .Select(degree => new Angle(degree.Theta + direction.Theta).GetDir())
                .ToArray();
        }

        private void OnGetTransitConnectableDirections(EntityUid uid, VentCrawlTransitComponent component, ref GetVentCrawlsConnectableDirectionsEvent args)
        {
            var rotation = Transform(uid).LocalRotation;
            var opposite = new Angle(rotation.Theta + Math.PI);

            args.Connectable = new[] { rotation.GetDir(), opposite.GetDir() };
        }

        private void OnDeconstruct(EntityUid uid, VentCrawlTubeComponent component, ConstructionBeforeDeleteEvent args)
        {
            DisconnectTube(uid, component);
        }

        private void OnStartup(EntityUid uid, VentCrawlTubeComponent component, ComponentStartup args)
        {
            UpdateAnchored(uid, component, Transform(uid).Anchored);
        }

        private void OnBreak(EntityUid uid, VentCrawlTubeComponent component, BreakageEventArgs args)
        {
            DisconnectTube(uid, component);
        }

        private void OnAnchorChange(EntityUid uid, VentCrawlTubeComponent component, ref AnchorStateChangedEvent args)
        {
            UpdateAnchored(uid, component, args.Anchored);
        }

        private void UpdateAnchored(EntityUid uid, VentCrawlTubeComponent component, bool anchored)
        {
            if (anchored)
            {
                ConnectTube(uid, component);
            }
            else
            {
                DisconnectTube(uid, component);
            }
        }

        private static void ConnectTube(EntityUid _, VentCrawlTubeComponent tube)
        {
            if (tube.Connected)
            {
                return;
            }

            tube.Connected = true;
        }


        private void DisconnectTube(EntityUid _, VentCrawlTubeComponent tube)
        {
            if (!tube.Connected)
            {
                return;
            }

            tube.Connected = false;

            var query = GetEntityQuery<VentCrawlHolderComponent>();
            foreach (var entity in tube.Contents.ContainedEntities.ToArray())
            {
                if (query.TryGetComponent(entity, out var holder))
                {
                    var Exitev = new VentCrawlExitEvent();
                    RaiseLocalEvent(entity, ref Exitev);
                }
            }
        }

        private bool TryInsert(EntityUid uid, EntityUid entity, VentCrawlEntryComponent? entry = null)
        {
            if (!Resolve(uid, ref entry))
                return false;

            if (!TryComp<VentCrawlerComponent>(entity, out var ventCrawlerComponent))
                return false;

            var holder = Spawn(VentCrawlEntryComponent.HolderPrototypeId, _transform.GetMapCoordinates(uid));
            var holderComponent = Comp<VentCrawlHolderComponent>(holder);

            _ventCrawableSystem.TryInsert(holder, entity, holderComponent);

            _mover.SetRelay(entity, holder);
            ventCrawlerComponent.InTube = true;
            Dirty(entity, ventCrawlerComponent);

            return _ventCrawableSystem.EnterTube(holder, uid, holderComponent);
        }
    }
}
