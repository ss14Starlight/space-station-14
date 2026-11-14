using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared._Starlight.Combat.OnHit;
using Content.Shared._Starlight.Reptilian.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Reptilian.Systems;

public sealed partial class ThickScalesSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private bool IsInstantInjector(EntityUid injector)
        => TryComp<InjectorComponent>(injector, out var comp)
        && _proto.Resolve(comp.ActiveModeProtoId, out InjectorModePrototype? mode)
        && mode.InjectOnUse;

    [SubscribeLocalEvent]
    private void OnBeforeInject(Entity<ThickScalesComponent> ent, ref TargetBeforeInjectEvent args)
    {
        if (!ent.Comp.BlockInstantInjectors
            || args.Cancelled
            || HasComp<PiercesThickScalesComponent>(args.UsedInjector)
            || !IsInstantInjector(args.UsedInjector))
            return;

        args.Cancel();
        args.OverrideMessage = Loc.GetString("thick-scales-injector-blocked",
            ("target", Identity.Entity(ent, EntityManager)),
            ("injector", args.UsedInjector));
    }

    [SubscribeLocalEvent]
    private void OnInjectorDoAfterTime(Entity<ThickScalesComponent> ent, ref InjectorMobDoAfterTimeEvent args)
    {
        // Drawing blood works as usual, only pushing reagents in is slowed down.
        if (!args.Mode.Behavior.HasFlag(InjectorBehavior.Inject) && !args.Mode.Behavior.HasFlag(InjectorBehavior.Dynamic))
            return;

        // Instant injectors are blocked outright when the injection happens.
        if (args.Mode.InjectOnUse)
            return;

        args.DoAfterTime *= ent.Comp.InjectTimeMultiplier;
        _popup.PopupClient(Loc.GetString("thick-scales-syringe-slow",
                ("target", Identity.Entity(ent, EntityManager))),
            ent,
            args.User,
            PopupType.SmallCaution);
    }

    [SubscribeLocalEvent]
    private void OnInjectOnHitAttempt(Entity<ThickScalesComponent> ent, ref InjectOnHitAttemptEvent args)
    {
        if (!ent.Comp.BlockRemoteInjection || args.Cancelled || HasComp<PiercesThickScalesComponent>(args.Used))
            return;

        args.Cancelled = true;
        PopupRemoteBlocked(ent, args.Attacker);
    }

    [SubscribeLocalEvent]
    private void OnSolutionInjectAttempt(Entity<ThickScalesComponent> ent, ref SolutionInjectAttemptEvent args)
    {
        if (!ent.Comp.BlockRemoteInjection || args.Cancelled)
            return;

        args.Cancelled = true;
        PopupRemoteBlocked(ent, args.Source);
    }

    private void PopupRemoteBlocked(EntityUid target, EntityUid? attacker)
    {
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString("thick-scales-remote-blocked-self"), target, target, PopupType.Small);

        if (attacker is { } source && source != target && Exists(source))
        {
            _popup.PopupEntity(Loc.GetString("thick-scales-remote-blocked-attacker",
                    ("target", Identity.Entity(target, EntityManager))),
                target,
                source,
                PopupType.SmallCaution);
        }
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<ThickScalesComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("thick-scales-examine", ("target", Identity.Entity(ent, EntityManager))));
    }
}
