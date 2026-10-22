using Content.Server._Starlight.GameTicking;
using Content.Server._Starlight.Roles;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Changeling;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private ChangelingRuleSystem _changelingRules = default!;
    [Dependency] private SharedRoleSystem _changelingRoles = default!;

    private static readonly EntProtoId FakeArmbladePrototype = "FakeArmBladeChangeling";
    private static readonly ProtoId<CurrencyPrototype> InfectionCurrency = "EvolutionPoint";
    private const int InfectionCost = 2;

    private void SubscribeStings()
    {
        SubscribeLocalEvent<ChangelingComponent, StingFakeArmbladeEvent>(OnStingFakeArmblade);
        SubscribeLocalEvent<ChangelingComponent, StingInfectEvent>(OnStingInfect);
        SubscribeLocalEvent<ChangelingInfectionComponent, ChangelingDevourAttemptEvent>(OnInfectionAttempt);
        SubscribeLocalEvent<ChangelingInfectionComponent, ChangelingDevourFinishedEvent>(OnInfectionFinished);
        SubscribeLocalEvent<ChangelingInfectionComponent, DoAfterAttemptEvent<ChangelingDevourWindupDoAfterEvent>>(OnInfectionWindupAttempt);
        SubscribeLocalEvent<ChangelingInfectionComponent, DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent>>(
            OnInfectionConsumeAttempt, before: [typeof(ChangelingDevourSystem)]);
    }

    private void OnStingFakeArmblade(EntityUid uid, ChangelingComponent comp, ref StingFakeArmbladeEvent args)
    {
        if (args.Handled || !TrySting(uid, args))
            return;
        var target = args.Target;
        var given = 0;
        for (var i = 0; i < (comp.Evolved ? 2 : 1); i++)
        {
            var blade = Spawn(FakeArmbladePrototype, Transform(target).Coordinates);
            if (comp.Evolved ? _hands.TryForcePickupAnyHand(target, blade) : _hands.TryPickupAnyHand(target, blade))
                given++;
            else
                QueueDel(blade);
        }
        if (given == 0)
        {
            UpdateChemicals(uid, comp, Comp<ChangelingActionComponent>(args.Action).ChemicalCost);
            _popup.PopupEntity(Loc.GetString("changeling-sting-fail-simplemob"), uid, uid);
            return;
        }
        PlayMeatySound(target, comp);
        args.Handled = true;
    }

    private void OnStingInfect(EntityUid uid, ChangelingComponent comp, ref StingInfectEvent args)
    {
        if (args.Handled || HasComp<ChangelingInfectionComponent>(uid)
            || !TryComp<ChangelingDevourComponent>(uid, out var devour) || devour.CurrentTarget != null)
            return;
        if (!HasInfectionPoints(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-infect-no-points"), uid, uid);
            return;
        }
        if (!CanInfect(args.Target) || !TryGetInfectionRule(out _)
            || !_mind.TryGetMind(uid, out var mind, out _)
            || !_mind.TryGetMind(args.Target, out var victimMind, out _))
        {
            _popup.PopupEntity(Loc.GetString("changeling-infect-invalid"), uid, uid);
            return;
        }

        AddComp(uid, new ChangelingInfectionComponent { Target = args.Target, Mind = mind, VictimMind = victimMind });
        if (!_devour.StartDevour((uid, devour), args.Target))
        {
            RemComp<ChangelingInfectionComponent>(uid);
            return;
        }
        args.Handled = true;
    }

    private bool CanInfect(EntityUid target)
        => IsPossessable(target) && _mind.TryGetMind(target, out var mind, out _)
           && !_changelingRoles.MindHasRole<SLChangelingRoleComponent>(mind);

    private bool HasInfectionPoints(EntityUid uid)
        => TryComp<StoreComponent>(uid, out var store)
           && store.Balance.TryGetValue(InfectionCurrency, out var points) && points >= InfectionCost;

    private bool TryGetInfectionRule(out SLChangelingRuleComponent rule)
    {
        var query = EntityQueryEnumerator<SLChangelingRuleComponent>();
        return query.MoveNext(out _, out rule!);
    }

    private bool CanContinueInfection(EntityUid uid, ChangelingInfectionComponent infection, EntityUid? target)
        => target == infection.Target && HasComp<ChangelingComponent>(uid) && _mobState.IsAlive(uid)
           && CanInfect(infection.Target) && HasInfectionPoints(uid)
           && _mind.TryGetMind(uid, out var mind, out _) && mind == infection.Mind
           && _mind.TryGetMind(infection.Target, out var victimMind, out _) && victimMind == infection.VictimMind;

    private void OnInfectionAttempt(EntityUid uid, ChangelingInfectionComponent infection, ref ChangelingDevourAttemptEvent args)
        => args.Cancelled |= !CanContinueInfection(uid, infection, args.Target);

    private void OnInfectionWindupAttempt(EntityUid uid, ChangelingInfectionComponent infection,
        ref DoAfterAttemptEvent<ChangelingDevourWindupDoAfterEvent> args)
    {
        if (!CanContinueInfection(uid, infection, args.DoAfter.Args.Target))
            args.Cancel();
    }

    private void OnInfectionConsumeAttempt(EntityUid uid, ChangelingInfectionComponent infection,
        ref DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent> args)
    {
        if (!CanContinueInfection(uid, infection, args.DoAfter.Args.Target))
            args.Cancel();
    }

    private void OnInfectionFinished(EntityUid uid, ChangelingInfectionComponent infection, ref ChangelingDevourFinishedEvent args)
    {
        if (args.Target != infection.Target)
            return;
        // Intercept completion before ordinary devour copies DNA or grants any absorption rewards.
        args.Handled = true;
        var valid = args.Completed && CanContinueInfection(uid, infection, args.Target);
        RemComp<ChangelingInfectionComponent>(uid);
        if (!valid || !TryGetInfectionRule(out var rule))
            return;

        var target = args.Target;
        if (!_changelingRules.MakeChangeling(target, rule, ChangelingRuleSystem.StartingEvolutionPoints / 2))
            return;

        var stasis = new EnterStasisEvent { Performer = target };
        OnEnterStasis(target, Comp<ChangelingComponent>(target), ref stasis);
        var store = Comp<StoreComponent>(uid);
        store.Balance[InfectionCurrency] -= InfectionCost;
        _store.UpdateUserInterface(uid, uid, store);
        _popup.PopupEntity(Loc.GetString("changeling-infect-success", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        _popup.PopupEntity(Loc.GetString("changeling-infect-target"), target, target);
    }
}

[RegisterComponent]
public sealed partial class ChangelingInfectionComponent : Component
{
    public EntityUid Target;
    public EntityUid Mind;
    public EntityUid VictimMind;
}
