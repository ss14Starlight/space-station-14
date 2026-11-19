using Content.Shared.Teleportation.Systems;
using Content.Shared.Anomaly.Components;
using Content.Shared.Verbs;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Content.Shared.Teleportation.Components;
using Content.Shared.Examine;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.Shadekin.Components;
using Robust.Shared.Network;
using Content.Shared.StatusEffectNew;
using Content.Shared._Starlight.Bluespace;

namespace Content.Shared._Starlight.Shadekin;

public abstract partial class SharedDarkPortalSystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _link = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize() => base.Initialize();

    [SubscribeLocalEvent]
    private void OnInit(Entity<DarkPortalComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        // Were looking for every hub the dark has.
        // Even tho there supose to be **ONLY ONE**!!
        // But hecc, thats works.
        // Also if there no hub, this portal will just do nothing.
        var query = EntityQueryEnumerator<DarkHubComponent>();
        while (query.MoveNext(out var target, out var portal))
            if (portal.Hub)
                _link.TryLink(ent, target);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<DarkPortalComponent> ent, ref AnomalyShutdownEvent args)
    {
        if (_net.IsClient)
            return;

        // We prevent Supercritical to Del the portal, However the others values checks here makes me... unhappy.
        if (args.Supercritical || ent.Comp.Brighteye is null || !TryComp<BrighteyeComponent>(ent.Comp.Brighteye.Value, out var brighteye))
            return;

        OnPortalShutdown((ent.Comp.Brighteye.Value, brighteye));
        QueueDel(ent);
    }

    /// <summary>
    /// Shutdown the Brighteye Portal correctly on both sides.
    /// By god dont call this on client.
    /// </summary>
    /// <param name="ent"></param>
    public void OnPortalShutdown(Entity<BrighteyeComponent> ent)
    {
        ent.Comp.Portal = null;
        _alerts.ShowAlert(ent.Owner, ent.Comp.PortalAlert);

        // Your a cultist now Brighteye... and you loss connection to the Dark! Hippie... wait no that bad!
        if (HasComp<CosmicCultComponent>(ent))
            return;

        _actionsSystem.AddAction(ent, ref ent.Comp.PortalAction, ent.Comp.BrighteyePortalAction, ent);
        _actionsSystem.SetCooldown(ent.Comp.PortalAction, TimeSpan.FromSeconds(600));
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DarkPortalComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Brighteye != args.Examiner)
            return;

        args.PushMarkup(Loc.GetString("shadekin-portal-owner"));
        if (!TryComp<AnomalyComponent>(ent, out var anomaly))
            return;

        var health = anomaly.Health; // Apprently if health is not a var, this shit breaks; why?

        args.PushMarkup(Loc.GetString("shadekin-portal-health-percentage", ("percent", health.ToString("P"))));
    }

    // Using OnAttemptPortal to apply an effect...
    // Because there no OnPortalEvent and im not touching Portal Upstream code again.
    // I already had to make OnAttemptPortalEvent; Deal with it.
    [SubscribeLocalEvent]
    private void OnAttemptPortal(Entity<DarkPortalComponent> ent, ref OnAttemptPortalEvent args)
    {
        if (HasComp<TheDarkImmuneComponent>(args.Subject))
            return;

        if (HasComp<ShadekinComponent>(args.Subject))
        {
            if (HasComp<BrighteyeComponent>(args.Subject))
                return;

            var ev = new NullSpaceShuntEvent();
            RaiseLocalEvent(args.Subject, ref ev);
            return;
        }

        // Yeah we apply Effect on portal use.
        _status.TryAddStatusEffectDuration(args.Subject, "StatusEffectTheDark", TimeSpan.FromSeconds(70));
    }

    [SubscribeLocalEvent]
    // Brighteye get to destroy their own portal, portal placement cooldown still applies.
    private void OnGetInteractionVerbs(EntityUid uid, DarkPortalComponent component, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || component.Brighteye != args.User || !HasComp<AnomalyComponent>(uid))
            return;

        var user = args.User;

        args.Verbs.Add(new InteractionVerb
        {
            Act = () =>
            {
                if (_net.IsClient) // Yep, this is first prize bullshit right here; But client is needed so we predict verbs.
                    return;

                if (TryComp<BrighteyeComponent>(user, out var brighteye))
                    OnPortalShutdown((user, brighteye));

                SpawnAtPosition(component.ShadekinShadow, Transform(uid).Coordinates);
                QueueDel(uid);
            },
            Text = Loc.GetString("shadekin-portal-destroy"),
        });
    }
}
