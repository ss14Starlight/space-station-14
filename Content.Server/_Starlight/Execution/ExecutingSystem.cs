using Content.Shared._Starlight.Execution;
using Content.Shared.Chat;
using Content.Shared.Clumsy;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Execution;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Kitchen.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;

namespace Content.Server._Starlight.Execution;

/// <summary>
///     Completes executions started by the predicted verbs in
///     <see cref="SharedExecutionSystem"/>.
/// </summary>
public sealed partial class ExecutionSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private SharedExecutionSystem _execution = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSuicideSystem _suicide = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, ExecutionDoAfterEvent>(OnExecutionDoAfterMelee);
        SubscribeLocalEvent<GunComponent, ExecutionDoAfterEvent>(OnExecutionDoAfterGun);
    }

    private void ShowExecutionInternalPopup(string locString, EntityUid attacker, EntityUid victim, EntityUid weapon, bool predict = true)
    {
        if (predict)
        {
            _popup.PopupEntity(
               Loc.GetString(locString, ("attacker", Identity.Entity(attacker, EntityManager)), ("victim", Identity.Entity(victim, EntityManager)), ("weapon", weapon)), attacker, Filter.Entities(attacker), true, PopupType.MediumCaution);
        }
        else
        {
            _popup.PopupEntity(
               Loc.GetString(locString, ("attacker", Identity.Entity(attacker, EntityManager)), ("victim", Identity.Entity(victim, EntityManager)), ("weapon", weapon)), attacker, Filter.Entities(attacker), true, PopupType.MediumCaution);
        }
    }

    private void ShowExecutionExternalPopup(string locString, EntityUid attacker, EntityUid victim, EntityUid weapon)
    {
        _popup.PopupEntity(
            Loc.GetString(locString, ("attacker", Identity.Entity(attacker, EntityManager)), ("victim", Identity.Entity(victim, EntityManager)), ("weapon", weapon)), attacker, Filter.PvsExcept(attacker), true, PopupType.MediumCaution);
    }

    private void OnExecutionDoAfterMelee(EntityUid entity, SharpComponent component, ref ExecutionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used == null || args.Target == null)
            return;

        if (!TryComp<MeleeWeaponComponent>(entity, out var meleeWeaponComp))
            return;

        var attacker = args.User;
        var victim = args.Target.Value;
        var weapon = args.Used.Value;

        if (!_execution.CanBeExecutedWithMelee(weapon, victim, attacker))
            return;

        // This is needed so the melee system does not stop it.
        var prev = _combat.IsInCombatMode(attacker);
        _combat.SetInCombatMode(attacker, true);

        var internalMsg = ExecutionComponent.CompleteInternalMeleeExecutionMessage;
        var externalMsg = ExecutionComponent.CompleteExternalMeleeExecutionMessage;

        if (attacker == victim)
        {

            if (!TryComp<DamageableComponent>(victim, out var damageableComponent))
                return;

            _audio.PlayPredicted(meleeWeaponComp.HitSound, victim, victim);
            _suicide.ApplyLethalDamage((victim, damageableComponent), meleeWeaponComp.Damage);
        }
        else
        {
            _damageableSystem.TryChangeDamage(victim, meleeWeaponComp.Damage * ExecutionComponent.DamageMultiplier, true);
        }

        _combat.SetInCombatMode(attacker, prev);
        args.Handled = true;

        if (attacker != victim)
        {
            ShowExecutionInternalPopup(internalMsg, attacker, victim, entity);
            ShowExecutionExternalPopup(externalMsg, attacker, victim, entity);
        }
        else
        {
            ShowExecutionInternalPopup(ExecutionComponent.CompleteInternalSelfMeleeExecutionMessage, victim, victim, entity, false);
            ShowExecutionExternalPopup(ExecutionComponent.CompleteExternalSelfMeleeExecutionMessage, victim, victim, entity);
        }
    }

    private void OnExecutionDoAfterGun(EntityUid uid, GunComponent component, ref ExecutionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used == null || args.Target == null)
            return;

        var attacker = args.User;
        var weapon = args.Used.Value;
        var victim = args.Target.Value;

        if (!_execution.CanBeExecutedWithGun(weapon, victim, attacker))
            return;

        // Check if any systems want to block our shot
        var prevention = new ShotAttemptedEvent
        {
            User = attacker,
            Used = (weapon, component),
        };

        RaiseLocalEvent(weapon, ref prevention);
        if (prevention.Cancelled)
            return;

        RaiseLocalEvent(attacker, ref prevention);
        if (prevention.Cancelled)
            return;

        // Not sure what this is for but gunsystem uses it so ehhh
        var attemptEv = new AttemptShootEvent(attacker, null);
        RaiseLocalEvent(weapon, ref attemptEv);

        if (attemptEv.Cancelled)
        {
            if (attemptEv.Message != null)
                _popup.PopupClient(attemptEv.Message, weapon, attacker);
            return;
        }

        // Take some ammunition for the shot (one bullet)
        var fromCoordinates = Transform(attacker).Coordinates;
        var ev = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), fromCoordinates, attacker);
        RaiseLocalEvent(weapon, ev);

        // Check if there's any ammo left
        if (ev.Ammo.Count <= 0)
        {
            _audio.PlayEntity(component.SoundEmpty, Filter.Pvs(weapon), weapon, true, AudioParams.Default);
            ShowExecutionExternalPopup("execution-popup-gun-empty", attacker, victim, weapon);
            return;
        }

        // Information about the ammo like damage
        DamageSpecifier damage = new DamageSpecifier();

        // Get some information from IShootable
        var ammoUid = ev.Ammo[0].Entity;
        switch (ev.Ammo[0].Shootable)
        {
            case CartridgeAmmoComponent cartridge:
                // Get the damage value
                var prototype = _prototypeManager.Index<EntityPrototype>(cartridge.Prototype);

                // Starlight start - support hitscans in cartridges
                if (prototype.TryGetComponent<HitscanAmmoComponent>(out var hitscan, _componentFactory))
                {
                    if (prototype.TryGetComponent<HitscanBasicDamageComponent>(out var hitscanDamage, _componentFactory))
                    {
                        damage = hitscanDamage.Damage;
                    }
                }
                // Starlight end
                prototype.TryGetComponent<ProjectileComponent>(out var projectileA, _componentFactory); // sloth forgive me
                if (projectileA != null)
                {
                    damage = projectileA.Damage;
                }
                prototype.TryGetComponent<ProjectileSpreadComponent>(out var projectilespreaderA, _componentFactory);
                if (projectilespreaderA != null)
                {
                    damage *= projectilespreaderA.Count;
                }

                // Expend the cartridge
                cartridge.Spent = true;
                _appearanceSystem.SetData(ammoUid!.Value, AmmoVisuals.Spent, true);
                Dirty(ammoUid.Value, cartridge);

                break;

            case AmmoComponent newAmmo:
                TryComp<ProjectileComponent>(ammoUid, out var projectileB);
                if (projectileB != null)
                {
                    damage = projectileB.Damage;
                }
                Del(ammoUid);
                break;

            case HitscanAmmoComponent:
                if (TryComp<HitscanBasicDamageComponent>(ammoUid, out var basicDamage))
                {
                    damage = basicDamage.Damage;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        // Clumsy people have a chance to shoot themselves
        if (TryComp<ClumsyComponent>(attacker, out var clumsy) && component.ClumsyProof == false)
        {
            if (_random.Prob(0.33333333f))
            {
                ShowExecutionInternalPopup("execution-popup-gun-clumsy-internal", attacker, victim, weapon);
                ShowExecutionExternalPopup("execution-popup-gun-clumsy-external", attacker, victim, weapon);

                // You shoot yourself with the gun (no damage multiplier)
                _damageableSystem.TryChangeDamage(attacker, damage, origin: attacker);
                _audio.PlayEntity(component.SoundGunshot, Filter.Pvs(weapon), weapon, true, AudioParams.Default);
                return;
            }
        }

        // Gun successfully fired, deal damage
        _damageableSystem.TryChangeDamage(victim, damage * ExecutionComponent.DamageMultiplier, true);
        _audio.PlayEntity(component.SoundGunshot, Filter.Pvs(weapon), weapon, false, AudioParams.Default);

        // Popups
        if (attacker != victim)
        {
            ShowExecutionInternalPopup(ExecutionComponent.CompleteInternalGunExecutionMessage, attacker, victim, weapon);
            ShowExecutionExternalPopup(ExecutionComponent.CompleteExternalGunExecutionMessage, attacker, victim, weapon);
        }
        else
        {
            ShowExecutionInternalPopup(ExecutionComponent.CompleteInternalSelfGunExecutionMessage, attacker, victim, weapon, false);
            ShowExecutionExternalPopup(ExecutionComponent.CompleteExternalSelfGunExecutionMessage, attacker, victim, weapon);
        }
    }
}
