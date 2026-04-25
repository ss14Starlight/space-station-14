using Content.Shared.Teleportation.Systems;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Anomaly.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Content.Shared.Anomaly;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Robust.Shared.Random;
using Content.Shared.Teleportation.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Examine;
using Content.Server.Anomaly;
using Content.Shared._Starlight.Railroading;
using Content.Server._Starlight.Railroading;
using Content.Shared.Light.Components;
using Content.Shared.Throwing;
using Content.Shared._Starlight.CosmicCult.Components;

namespace Content.Server._Starlight.Shadekin;

public sealed class DarkPortalSystem : EntitySystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PoweredLightSystem _light = default!;
    [Dependency] private readonly ShadekinSystem _shadekin = default!;
    [Dependency] private readonly SharedAnomalySystem _sharedAnomalySystem = default!;
    [Dependency] private readonly AnomalySystem _anomalySystem = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RailroadingSupercritPortalSystem _railroadingSupercritPortal = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DarkPortalComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<DarkPortalComponent, AnomalyPulseEvent>(OnPulse);
        SubscribeLocalEvent<DarkPortalComponent, AnomalySupercriticalEvent>(OnSupercritical);
        SubscribeLocalEvent<DarkPortalComponent, AnomalyShutdownEvent>(OnShutdown);

        SubscribeLocalEvent<DarkPortalComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<DarkPortalComponent, OnAttemptPortalEvent>(OnAttemptPortal);
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

        foreach (var ent in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, range))
            _light.TryDestroyBulb(ent);

        int newenergy = _random.Next(5, 30) * (int)args.Stability * (int)args.PowerModifier;

        foreach (var ent in _lookup.GetEntitiesInRange<BrighteyeComponent>(Transform(uid).Coordinates, range))
        {
            ent.Comp.Energy = Math.Clamp(ent.Comp.Energy + newenergy, 0, ent.Comp.MaxEnergy);
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnSupercritical(EntityUid uid, DarkPortalComponent component, ref AnomalySupercriticalEvent args)
    {
        var range = component.PulseRange * 3 * args.PowerModifier;

        foreach (var ent in _lookup.GetEntitiesInRange<PoweredLightComponent>(Transform(uid).Coordinates, range))
            _light.TryDestroyBulb(ent.Owner, ent.Comp);

        foreach (var ent in _lookup.GetEntitiesInRange<BrighteyeComponent>(Transform(uid).Coordinates, range))
        {
            ent.Comp.Energy = ent.Comp.MaxEnergy;
            Dirty(ent.Owner, ent.Comp);
        }

        // Objectives
        if (component.Brighteye is not null && HasComp<RailroadSupercritPortalWatcherComponent>(component.Brighteye.Value))
            _railroadingSupercritPortal.SupercriticalTask(component.Brighteye.Value);

        if (TryComp<AnomalyComponent>(uid, out var anomaly))
        {
            _sharedAnomalySystem.ChangeAnomalyStability(uid, -0.5f, anomaly);
            _sharedAnomalySystem.ChangeAnomalySeverity(uid, -0.5f, anomaly);
            _sharedAnomalySystem.ChangeAnomalyHealth(uid, 1f, anomaly);
            _anomalySystem.ShuffleParticlesEffect((uid, anomaly));
        }
    }

    private void OnShutdown(EntityUid uid, DarkPortalComponent component, ref AnomalyShutdownEvent args)
    {
        if (args.Supercritical || component.Brighteye is null || !TryComp<BrighteyeComponent>(component.Brighteye.Value, out var brighteye))
            return;

        OnPortalShutdown(component.Brighteye.Value, brighteye);
        QueueDel(uid);
    }

    public void OnPortalShutdown(EntityUid uid, BrighteyeComponent component)
    {
        component.Portal = null;
        _alerts.ShowAlert(uid, component.PortalAlert);

        if (HasComp<CosmicCultComponent>(uid))
            return;

        _actionsSystem.AddAction(uid, ref component.PortalAction, component.BrighteyePortalAction, uid);
        _actionsSystem.SetCooldown(component.PortalAction, TimeSpan.FromSeconds(300));
    }

    private void OnExamined(EntityUid uid, DarkPortalComponent component, ref ExaminedEvent args)
    {
        if (component.Brighteye != args.Examiner)
            return;

        args.PushMarkup(Loc.GetString("shadekin-portal-owner"));
        if (TryComp<AnomalyComponent>(uid, out var anomaly))
        {
            if (anomaly.Stability > anomaly.GrowthThreshold)
                args.PushMarkup(Loc.GetString("shadekin-portal-stability-unstable"));
            else
                args.PushMarkup(Loc.GetString("shadekin-portal-stability-stable"));

            var severity = anomaly.Severity;
            var health = anomaly.Health;

            args.PushMarkup(Loc.GetString("anomaly-scanner-severity-percentage", ("percent", severity.ToString("P"))));
            args.PushMarkup(Loc.GetString("shadekin-portal-health-percentage", ("percent", health.ToString("P"))));
        }
    }

    // APPRENTLY... MOVING THIS TO SHARED IS NOT TRIGGERED? SO I HAVE TO FUCKING COPY/PASTE ON CLIENT? WTF?
    private void OnAttemptPortal(EntityUid uid, DarkPortalComponent component, OnAttemptPortalEvent args)
    {
        if (HasComp<BrighteyeComponent>(args.Subject))
            return;

        // TODO: Check if we have the Nullspace Suit? (also works for pull and thrown)

        if (TryComp<PullableComponent>(args.Subject, out var pullablea) && pullablea.BeingPulled && HasComp<BrighteyeComponent>(pullablea.Puller))
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

        args.Verbs.Add(new()
        {
            Act = () =>
            {
                if (TryComp<BrighteyeComponent>(user, out var brighteye))
                    OnPortalShutdown(user, brighteye);

                SpawnAtPosition(component.ShadekinShadow, Transform(uid).Coordinates);
                QueueDel(uid);
            },
            Text = Loc.GetString("shadekin-portal-destroy"),
        });

        if (TryComp<BrighteyeComponent>(user, out var brighteye))
        {
            args.Verbs.Add(new()
            {
                Act = () =>
                {
                    if (_shadekin.OnAttemptEnergyUse(user, brighteye, 50))
                    {
                        _sharedAnomalySystem.ChangeAnomalyStability(uid, -0.15f, anomaly);
                        _sharedAnomalySystem.ChangeAnomalySeverity(uid, -0.15f, anomaly);
                        _sharedAnomalySystem.ChangeAnomalyHealth(uid, 0.3f, anomaly);
                    }
                },
                Text = Loc.GetString("shadekin-portal-stabilize"),
                Message = brighteye.Energy < component.StabilizeCost ? Loc.GetString("shadekin-noenergy") : Loc.GetString("shadekin-portal-stabilize-info"),
                Disabled = brighteye.Energy < component.StabilizeCost,
            });
        }
    }
}
