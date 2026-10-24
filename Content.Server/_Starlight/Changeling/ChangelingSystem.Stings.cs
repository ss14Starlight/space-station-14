using Content.Server._Starlight.GameTicking;
using Content.Server._Starlight.Roles;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Body.Components;
using Content.Shared.Changeling;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Tag;
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
    private static readonly ProtoId<TagPrototype> IpcTag = "IPC";

    [SubscribeLocalEvent]
    private void OnStingFakeArmblade(EntityUid uid, ChangelingComponent comp, ref StingFakeArmbladeEvent args)
    {
        if (args.Handled || !TrySting(uid, args, true))
            return;
        var target = args.Target;
        var given = 0;
        for (var i = 0; i < (comp.Evolved ? 2 : 1); i++)
        {
            var blade = Spawn(FakeArmbladePrototype, Transform(target).Coordinates);
            if (comp.Evolved
                ? _hands.TryForcePickupAnyHand(target, blade, checkActionBlocker: false)
                : _hands.TryPickupAnyHand(target, blade, checkActionBlocker: false))
                given++;
            else
                QueueDel(blade);
        }
        if (given == 0)
        {
            _popup.PopupEntity(Loc.GetString("changeling-sting-fail-simplemob"), uid, uid);
            return;
        }
        PlayMeatySound(target, comp);
        _popup.PopupEntity(Loc.GetString("changeling-sting", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
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

    private bool IsPossessable(EntityUid target)
        => !TerminatingOrDeleted(target) && HasComp<BodyComponent>(target) && HasComp<BloodstreamComponent>(target)
           && !HasComp<ChangelingComponent>(target) && !HasComp<ChangelingColonyMemberComponent>(target)
           && !HasComp<AbsorbedComponent>(target) && !_tag.HasTag(target, IpcTag);

    private bool CanInfect(EntityUid target)
        => IsPossessable(target) && _mind.TryGetMind(target, out var mind, out _)
           && !_changelingRoles.MindHasRole<SLChangelingRoleComponent>(mind);

    [SubscribeLocalEvent]
    private void OnStingAssimilate(EntityUid uid, ChangelingComponent comp, ref StingAssimilateEvent args)
    {
        if (args.Handled || HasComp<ChangelingInfectionComponent>(uid)
            || !TryComp<ChangelingDevourComponent>(uid, out var devour) || devour.CurrentTarget != null
            || !TryGetColony(uid, out var member, out var colony) || !colony.Learned
            || member.IsFragment || !IsHivePlayer(uid, colony))
            return;
        if (comp.Biomass < comp.MaxBiomass / 4)
        {
            _popup.PopupEntity(Loc.GetString("changeling-assimilate-no-biomass"), uid, uid);
            return;
        }
        if (CountCarriers(colony) >= CarrierLimit)
        {
            _popup.PopupEntity(Loc.GetString("changeling-assimilate-limit"), uid, uid);
            return;
        }
        if (!CanInfect(args.Target) || !_mind.TryGetMind(uid, out var mind, out _)
            || !_mind.TryGetMind(args.Target, out var victimMind, out _))
        {
            _popup.PopupEntity(Loc.GetString("changeling-assimilate-invalid"), uid, uid);
            return;
        }

        AddComp(uid, new ChangelingInfectionComponent
        {
            Target = args.Target,
            Mind = mind,
            VictimMind = victimMind,
            Assimilating = true,
        });
        if (!_devour.StartDevour((uid, devour), args.Target))
        {
            RemComp<ChangelingInfectionComponent>(uid);
            return;
        }
        args.Handled = true;
    }

    private bool CanContinueAssimilation(EntityUid uid, ChangelingComponent comp)
        => TryGetColony(uid, out var member, out var colony) && colony.Learned && !member.IsFragment
           && IsHivePlayer(uid, colony) && CountCarriers(colony) < CarrierLimit
           && comp.Biomass >= comp.MaxBiomass / 4;

    private bool HasInfectionPoints(EntityUid uid)
        => TryComp<StoreComponent>(uid, out var store)
           && store.Balance.TryGetValue(InfectionCurrency, out var points) && points >= InfectionCost;

    private bool TryGetInfectionRule(out SLChangelingRuleComponent rule)
    {
        var query = EntityQueryEnumerator<SLChangelingRuleComponent>();
        return query.MoveNext(out _, out rule!);
    }

    private bool CanContinueInfection(EntityUid uid, ChangelingInfectionComponent infection, EntityUid? target)
        => target == infection.Target && TryComp<ChangelingComponent>(uid, out var comp) && _mobState.IsAlive(uid)
           && CanInfect(infection.Target)
           && (infection.Assimilating ? CanContinueAssimilation(uid, comp) : HasInfectionPoints(uid))
           && _mind.TryGetMind(uid, out var mind, out _) && mind == infection.Mind
           && _mind.TryGetMind(infection.Target, out var victimMind, out _) && victimMind == infection.VictimMind;

    [SubscribeLocalEvent]
    private void OnInfectionAttempt(EntityUid uid, ChangelingInfectionComponent infection, ref ChangelingDevourAttemptEvent args)
        => args.Cancelled |= !CanContinueInfection(uid, infection, args.Target);

    [SubscribeLocalEvent]
    private void OnInfectionWindupAttempt(EntityUid uid, ChangelingInfectionComponent infection,
        ref DoAfterAttemptEvent<ChangelingDevourWindupDoAfterEvent> args)
    {
        if (!CanContinueInfection(uid, infection, args.DoAfter.Args.Target))
            args.Cancel();
    }

    [SubscribeLocalEvent(before: [typeof(ChangelingDevourSystem)])]
    private void OnInfectionConsumeAttempt(EntityUid uid, ChangelingInfectionComponent infection,
        ref DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent> args)
    {
        if (!CanContinueInfection(uid, infection, args.DoAfter.Args.Target))
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnInfectionFinished(EntityUid uid, ChangelingInfectionComponent infection, ref ChangelingDevourFinishedEvent args)
    {
        if (args.Target != infection.Target)
            return;
        // Intercept completion before ordinary devour copies DNA or grants any absorption rewards.
        args.Handled = true;
        var valid = args.Completed && CanContinueInfection(uid, infection, args.Target);
        RemComp<ChangelingInfectionComponent>(uid);
        if (!valid)
            return;
        if (infection.Assimilating)
        {
            AssimilateHiveBody(uid, Comp<ChangelingComponent>(uid), args.Target, infection.VictimMind);
            return;
        }
        if (!TryGetInfectionRule(out var rule))
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

    private void AssimilateHiveBody(EntityUid uid, ChangelingComponent comp, EntityUid target, EntityUid victimMind)
    {
        if (!TryGetColony(uid, out var member, out var colony))
            return;
        AbsorbHiveMind(uid, target);
        if (!colony.Minds.TryGetValue(victimMind, out var anchor)
            || !_mind.TryGetMind(anchor, out var transferredMind, out _) || transferredMind != victimMind)
            return;

        _damage.SetAllDamage(target, FixedPoint2.Zero);
        var blood = Comp<BloodstreamComponent>(target);
        _blood.TryModifyBleedAmount((target, blood), -blood.BleedAmount);
        _blood.TryModifyBloodLevel((target, blood), blood.BloodReferenceSolution.Volume);
        _mobState.ChangeMobState(target, MobState.Alive);
        AddColonyMember(target, member.Mind, colony);
        ActivateVacantCarrier(target);
        UpdateBiomass(uid, comp, -comp.MaxBiomass / 4);
        UpdateColonyUi(uid, colony);
        _popup.PopupEntity(Loc.GetString("changeling-assimilate-success", ("target", Identity.Entity(target, EntityManager))), uid, uid);
    }
}

[RegisterComponent]
public sealed partial class ChangelingInfectionComponent : Component
{
    public EntityUid Target;
    public EntityUid Mind;
    public EntityUid VictimMind;
    public bool Assimilating;
}
