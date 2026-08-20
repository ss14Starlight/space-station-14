using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Execution;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Starlight.Execution;

/// <summary>
///     Predicted verbs for violently murdering cuffed or crit creatures.
/// </summary>
public sealed class SharedExecutionSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gunSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, GetVerbsEvent<UtilityVerb>>(OnGetInteractionVerbsMelee);
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<UtilityVerb>>(OnGetInteractionVerbsGun);
    }

    private void OnGetInteractionVerbsMelee(EntityUid uid, SharpComponent comp, GetVerbsEvent<UtilityVerb> args)
    {
        if (args.Hands == null || args.Using == null || !args.CanAccess || !args.CanInteract)
            return;

        var attacker = args.User;
        var weapon = args.Using!.Value;
        var victim = args.Target;

        if (!CanBeExecutedWithMelee(weapon, victim, attacker))
            return;

        UtilityVerb verb = new()
        {
            Act = () => TryStartExecutionDoAfter(weapon, victim, attacker),
            Impact = LogImpact.High,
            Text = attacker == victim ? Loc.GetString("suicide-verb-name") : Loc.GetString("execution-verb-name"),
            Message = attacker == victim ? Loc.GetString("suicide-verb-message") : Loc.GetString("execution-verb-message"),
        };

        args.Verbs.Add(verb);
    }

    private void OnGetInteractionVerbsGun(EntityUid uid, GunComponent comp, GetVerbsEvent<UtilityVerb> args)
    {
        if (args.Hands == null || args.Using == null || !args.CanAccess || !args.CanInteract)
            return;

        var attacker = args.User;
        var weapon = args.Using!.Value;
        var victim = args.Target;

        if (!CanBeExecutedWithGun(weapon, victim, attacker))
            return;

        UtilityVerb verb = new()
        {
            Act = () => TryStartGunExecutionDoafter(weapon, victim, attacker),
            Impact = LogImpact.High,
            Text = attacker == victim ? Loc.GetString("suicide-verb-name") : Loc.GetString("execution-verb-name"),
            Message = attacker == victim ? Loc.GetString("suicide-verb-message") : Loc.GetString("execution-verb-message"),
        };

        args.Verbs.Add(verb);
    }

    private void TryStartExecutionDoAfter(EntityUid weapon, EntityUid victim, EntityUid attacker)
    {
        if (!CanBeExecutedWithMelee(weapon, victim, attacker))
            return;

        if (attacker == victim)
        {
            ShowExecutionStartPopup(ExecutionComponent.InternalSelfMeleeExecutionMessage, ExecutionComponent.ExternalSelfMeleeExecutionMessage, attacker, victim, weapon);
        }
        else
        {
            ShowExecutionStartPopup(ExecutionComponent.InternalMeleeExecutionMessage, ExecutionComponent.ExternalMeleeExecutionMessage, attacker, victim, weapon);
        }

        var doAfter =
            new DoAfterArgs(EntityManager, attacker, ExecutionComponent.MeleeDoAfterDuration, new ExecutionDoAfterEvent(), weapon, target: victim, used: weapon)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true
            };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void TryStartGunExecutionDoafter(EntityUid weapon, EntityUid victim, EntityUid attacker)
    {
        if (!CanBeExecutedWithGun(weapon, victim, attacker))
            return;

        if (!TryComp<GunComponent>(weapon, out var gunComponent))
            return;

        var shotAttempted = new ShotAttemptedEvent
        {
            User = attacker,
            Used = (weapon, gunComponent),
        };
        RaiseLocalEvent(weapon, ref shotAttempted);
        if (shotAttempted.Cancelled)
        {
            if (shotAttempted.Message != null)
                _popup.PopupClient(shotAttempted.Message, weapon, attacker);
            return;
        }

        if (attacker == victim)
        {
            ShowExecutionStartPopup(ExecutionComponent.InternalSelfGunExecutionMessage, ExecutionComponent.ExternalSelfGunExecutionMessage, attacker, victim, weapon);
        }
        else
        {
            ShowExecutionStartPopup(ExecutionComponent.InternalGunExecutionMessage, ExecutionComponent.ExternalGunExecutionMessage, attacker, victim, weapon);
        }

        var doAfter =
            new DoAfterArgs(EntityManager, attacker, ExecutionComponent.GunDoAfterDuration, new ExecutionDoAfterEvent(), weapon, target: victim, used: weapon)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true
            };

        _doAfter.TryStartDoAfter(doAfter);
    }

    public bool CanBeExecutedWithAny(EntityUid victim, EntityUid attacker)
    {
        // No point executing someone if they can't take damage
        if (!HasComp<DamageableComponent>(victim))
            return false;

        // You can't execute something that cannot die
        if (!TryComp<MobStateComponent>(victim, out var mobState))
            return false;

        // You're not allowed to execute dead people (no fun allowed)
        if (_mobState.IsDead(victim, mobState))
            return false;

        // You must be able to attack people to execute
        if (!_actionBlocker.CanAttack(attacker, victim))
            return false;

        // The victim must be incapacitated to be executed
        if (victim != attacker && _actionBlocker.CanInteract(victim, null))
            return false;

        // All checks passed
        return true;
    }

    public bool CanBeExecutedWithMelee(EntityUid weapon, EntityUid victim, EntityUid user)
    {
        if (!CanBeExecutedWithAny(victim, user))
            return false;

        // We must be able to actually hurt people with the weapon
        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee) || melee.Damage.GetTotal() <= FixedPoint2.Zero)
            return false;

        return true;
    }

    public bool CanBeExecutedWithGun(EntityUid weapon, EntityUid victim, EntityUid user)
    {
        if (!CanBeExecutedWithAny(victim, user))
            return false;

        // We must be able to actually fire the gun
        if (!TryComp<GunComponent>(weapon, out var gun) || !_gunSystem.CanShoot(gun))
            return false;

        if (_appearanceSystem.TryGetData(weapon, AmmoVisuals.BoltClosed, out bool boltClosed))
            if (!boltClosed)
                return false;

        return true;
    }

    private void ShowExecutionStartPopup(string internalLocString, string externalLocString, EntityUid attacker, EntityUid victim, EntityUid weapon)
    {
        var internalMsg = Loc.GetString(internalLocString, ("attacker", Identity.Entity(attacker, EntityManager)), ("victim", Identity.Entity(victim, EntityManager)), ("weapon", weapon));
        var externalMsg = Loc.GetString(externalLocString, ("attacker", Identity.Entity(attacker, EntityManager)), ("victim", Identity.Entity(victim, EntityManager)), ("weapon", weapon));
        _popup.PopupPredicted(internalMsg, externalMsg, attacker, attacker, PopupType.MediumCaution);
    }
}
