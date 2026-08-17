using Content.Shared.Teleportation.Systems;
using Content.Shared.Anomaly.Components;
using Content.Shared.Verbs;
using Content.Shared.Anomaly;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Robust.Shared.Random;
using Content.Shared.Teleportation.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Examine;
using Content.Shared.Light.Components;
using Content.Shared.Throwing;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared._Starlight.Railroading.Components.Watchers;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Shadekin;

public sealed partial class DarkPortalSystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _link = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPoweredLightSystem _light = default!;
    [Dependency] private ShadekinSystem _shadekin = default!;
    [Dependency] private SharedAnomalySystem _anomalySystem = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RailroadingSupercritPortalSystem _railroadingSupercritPortal = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DarkPortalComponent, OnAttemptPortalEvent>(OnAttemptPortal);

        // Portal entry checks run on both sides, but portal state and anomaly effects are server-authoritative.
        if (_net.IsClient)
            return;

        SubscribeLocalEvent<DarkPortalComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<DarkPortalComponent, AnomalyPulseEvent>(OnPulse);
        SubscribeLocalEvent<DarkPortalComponent, AnomalySupercriticalEvent>(OnSupercritical);
        SubscribeLocalEvent<DarkPortalComponent, AnomalyShutdownEvent>(OnShutdown);

        SubscribeLocalEvent<DarkPortalComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<DarkPortalComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(EntityUid uid, DarkPortalComponent component, ComponentStartup args)
    {
        var query = EntityQueryEnumerator<DarkHubComponent>();
        while (query.MoveNext(out var target, out var portal))
            if (portal.Hub)
                _link.TryLink(uid, target);
    }

    private void OnPulse(EntityUid uid, DarkPortalComponent component, ref AnomalyPulseEvent args)
    {
        var range = component.PulseRange * args.Stability * args.PowerModifier;

        var xform = Transform(uid);
        foreach (var ent in _lookup.GetEntitiesInRange(xform.Coordinates, range))
            _light.TryDestroyBulb(ent);

        var newEnergy = _random.Next(5, 30) * (int)args.Stability * (int)args.PowerModifier;

        foreach (var ent in _lookup.GetEntitiesInRange<BrighteyeComponent>(xform.Coordinates, range))
        {
            ent.Comp.Energy = Math.Clamp(ent.Comp.Energy + newEnergy, 0, ent.Comp.MaxEnergy);
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnSupercritical(EntityUid uid, DarkPortalComponent component, ref AnomalySupercriticalEvent args)
    {
        var range = component.PulseRange * 3 * args.PowerModifier;

        var xform = Transform(uid);
        foreach (var ent in _lookup.GetEntitiesInRange<PoweredLightComponent>(xform.Coordinates, range))
            _light.TryDestroyBulb(ent.Owner, ent.Comp);

        foreach (var ent in _lookup.GetEntitiesInRange<BrighteyeComponent>(xform.Coordinates, range))
        {
            ent.Comp.Energy = ent.Comp.MaxEnergy;
            Dirty(ent.Owner, ent.Comp);
        }

        // Objectives
        if (component.Brighteye is not null && HasComp<RailroadSupercritPortalWatcherComponent>(component.Brighteye.Value))
            _railroadingSupercritPortal.SupercriticalTask(component.Brighteye.Value);

        if (!TryComp<AnomalyComponent>(uid, out var anomaly))
            return;

        _anomalySystem.ChangeAnomalyStability(uid, -0.5f, anomaly);
        _anomalySystem.ChangeAnomalySeverity(uid, -0.5f, anomaly);
        _anomalySystem.ChangeAnomalyHealth(uid, 1f, anomaly);
        _anomalySystem.ShuffleParticlesEffect((uid, anomaly));
    }

    private void OnShutdown(Entity<DarkPortalComponent> ent, ref AnomalyShutdownEvent args)
    {
        if (args.Supercritical || ent.Comp.Brighteye is null || !TryComp<BrighteyeComponent>(ent.Comp.Brighteye.Value, out var brighteye))
            return;

        OnPortalShutdown((ent.Comp.Brighteye.Value, brighteye));
        PredictedQueueDel(ent);
    }

    public void OnPortalShutdown(Entity<BrighteyeComponent> ent)
    {
        ent.Comp.Portal = null;
        _alerts.ShowAlert(ent.Owner, ent.Comp.PortalAlert);

        if (HasComp<CosmicCultComponent>(ent))
            return;

        _actionsSystem.AddAction(ent, ref ent.Comp.PortalAction, ent.Comp.BrighteyePortalAction, ent);
        _actionsSystem.SetCooldown(ent.Comp.PortalAction, TimeSpan.FromSeconds(300));
    }

    private void OnExamined(Entity<DarkPortalComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Brighteye != args.Examiner)
            return;

        args.PushMarkup(Loc.GetString("shadekin-portal-owner"));
        if (!TryComp<AnomalyComponent>(ent, out var anomaly))
            return;

        args.PushMarkup(anomaly.Stability > anomaly.GrowthThreshold
            ? Loc.GetString("shadekin-portal-stability-unstable")
            : Loc.GetString("shadekin-portal-stability-stable"));

        var severity = anomaly.Severity;
        var health = anomaly.Health;

        args.PushMarkup(Loc.GetString("anomaly-scanner-severity-percentage", ("percent", severity.ToString("P"))));
        args.PushMarkup(Loc.GetString("shadekin-portal-health-percentage", ("percent", health.ToString("P"))));
    }

    private void OnAttemptPortal(Entity<DarkPortalComponent> ent, ref OnAttemptPortalEvent args)
    {
        if (HasComp<BrighteyeComponent>(args.Subject))
            return;

        // TODO: Check if we have the Nullspace Suit? (also works for pull and thrown)

        if (TryComp<PullableComponent>(args.Subject, out var pullable) && pullable.BeingPulled && HasComp<BrighteyeComponent>(pullable.Puller))
            return;

        if (TryComp<ThrownItemComponent>(args.Subject, out var thrown) && HasComp<BrighteyeComponent>(thrown.Thrower))
            return;

        args.Cancel();
    }

    private void OnGetInteractionVerbs(EntityUid uid, DarkPortalComponent component, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || component.Brighteye != args.User || !TryComp<AnomalyComponent>(uid, out var anomaly))
            return;

        var user = args.User;

        args.Verbs.Add(new InteractionVerb
        {
            Act = () =>
            {
                if (TryComp<BrighteyeComponent>(user, out var brighteye))
                    OnPortalShutdown((user, brighteye));

                PredictedSpawnAtPosition(component.ShadekinShadow, Transform(uid).Coordinates);
                PredictedQueueDel(uid);
            },
            Text = Loc.GetString("shadekin-portal-destroy"),
        });

        if (!TryComp<BrighteyeComponent>(user, out var brighteye))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Act = () =>
            {
                if (!_shadekin.OnAttemptEnergyUse(user, brighteye, 50))
                    return;

                _anomalySystem.ChangeAnomalyStability(uid, -0.15f, anomaly);
                _anomalySystem.ChangeAnomalySeverity(uid, -0.15f, anomaly);
                _anomalySystem.ChangeAnomalyHealth(uid, 0.3f, anomaly);
            },
            Text = Loc.GetString("shadekin-portal-stabilize"),
            Message = brighteye.Energy < component.StabilizeCost ? Loc.GetString("shadekin-noenergy") : Loc.GetString("shadekin-portal-stabilize-info"),
            Disabled = brighteye.Energy < component.StabilizeCost,
        });
    }
}
