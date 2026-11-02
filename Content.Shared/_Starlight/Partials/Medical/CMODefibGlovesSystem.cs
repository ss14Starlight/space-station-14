// ReSharper disable CheckNamespace
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Electrocution;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Strip.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Medical;

/// <summary>Handles defibrillation, contact shocks, comedy, and toggling for the prototype resuscitation gloves.</summary>
public sealed partial class CMODefibGlovesSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> ShockDamageType = "Shock";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DamageExamineSystem _damageExamine = default!;
    [Dependency] private SharedDefibrillatorSystem _defib = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMeleeWeaponSystem _meleeWeapon = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Shows shock damage in examine text while active, and melee damage while off.
    [SubscribeLocalEvent]
    private void OnDamageExamine(Entity<CMODefibGlovesComponent> ent, ref DamageExamineEvent args)
    {
        DamageSpecifier damage;
        if (_toggle.IsActivated(ent.Owner) && TryComp<DefibrillatorComponent>(ent.Owner, out var defib))
            damage = new DamageSpecifier(_prototypes.Index(ShockDamageType), defib.ZapDamage);
        else if (TryComp<MeleeWeaponComponent>(ent.Owner, out var melee))
            damage = _damageable.ApplyUniversalAllModifiers(_meleeWeapon.GetDamage(ent.Owner, args.User, melee));
        else
            return;

        _damageExamine.AddDamageExamine(args.Message, damage, Loc.GetString("damage-melee"));
    }

    // Conditionals to make sure it works as a defib on a dead body
    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<CMODefibGlovesComponent> ent, ref CMODefibGlovesDoAfterEvent args)
    {
        if (!_net.IsServer || args.Handled || args.Cancelled || args.Target is not { } target ||
            !IsWorn(ent.Owner, args.User) || !CanZap(ent.Owner, target, args.User) ||
            !_mobState.IsDead(target) || !_defib.CanZap(ent.Owner, target, args.User))
            return;

        args.Handled = true;
        _defib.Zap(ent.Owner, target, args.User);
    }

    [SubscribeLocalEvent]
    private void OnToggled(Entity<CMODefibGlovesComponent> ent, ref ItemToggledEvent args)
    {
        if (TryComp<ToggleClothingComponent>(ent.Owner, out var clothing))
            _actions.SetToggled(clothing.ActionEntity, args.Activated);

        // Turning them on while holding them is a bad idea.
        if (_net.IsServer && args.Activated && args.User is { } user &&
            _hands.IsHolding(user, ent.Owner))
            ShockHandler(ent.Owner, user);
    }

    [SubscribeLocalEvent]
    private void OnGetActivationVerbs(Entity<CMODefibGlovesComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var activated = _toggle.IsActivated(ent.Owner);
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString(activated ? "cmo-defib-gloves-turn-off" : "cmo-defib-gloves-turn-on"),
            Act = () =>
            {
                // If they're unequipped or on the floor, trying to turn them off without insuls shocks you.
                if (_toggle.IsActivated(ent.Owner))
                {
                    if (IsWorn(ent.Owner, user) || HasInsulatedGloves(user))
                        _toggle.TryDeactivate(ent.Owner, user);
                    else if (_net.IsServer)
                        ShockHandler(ent.Owner, user);
                }
                else
                {
                    _toggle.TryActivate(ent.Owner, user);
                }
            },
        });
    }

    // Trying to eject the power cell on active gloves shocks you.
    [SubscribeLocalEvent]
    private void OnGetCellEjectVerb(Entity<CMODefibGlovesComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_toggle.IsActivated(ent.Owner) ||
            HasInsulatedGloves(args.User) || !_powerCell.TryGetBatteryFromSlot(ent.Owner, out _))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("power-cell-slot-component-slot-name-default"),
            Category = VerbCategory.Eject,
            Act = () =>
            {
                if (_net.IsServer && _toggle.IsActivated(ent.Owner) && !HasInsulatedGloves(user))
                    ShockHandler(ent.Owner, user);
            },
        });
    }

    // Show real eject option if the user is insulated.
    [SubscribeLocalEvent]
    private void OnCellEjectAttempt(Entity<CMODefibGlovesComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.SlotEntity == ent.Owner &&
            TryComp<PowerCellSlotComponent>(ent.Owner, out var slot) && args.Slot.ID == slot.CellSlotId &&
            _toggle.IsActivated(ent.Owner) && args.User is { } user && !HasInsulatedGloves(user))
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnUnequipped(Entity<CMODefibGlovesComponent> ent, ref GotUnequippedEvent args)
    {
        if (!args.SlotFlags.HasFlag(SlotFlags.GLOVES) ||
            TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.EquipTarget))
            return;
        // Taking them off while they're activated is a bad idea
        if (_net.IsServer && _toggle.IsActivated(ent.Owner))
        {
            _transform.DropNextTo(ent.Owner, args.EquipTarget);
            ShockHandler(ent.Owner, args.EquipTarget);
        }

        if (!TryComp<DoAfterComponent>(args.EquipTarget, out var doAfters))
            return;

        foreach (var doAfter in doAfters.DoAfters.Values)
            if (doAfter.Args.Event is CMODefibGlovesDoAfterEvent)
                _doAfter.Cancel(args.EquipTarget, doAfter.Index);
    }

    // Trying to pick up activated gloves shocks you.
    [SubscribeLocalEvent]
    private void OnBeforePickedUp(Entity<CMODefibGlovesComponent> ent, ref BeforeGettingEquippedHandEvent args)
    {
        if (!_net.IsServer || !_toggle.IsActivated(ent.Owner) || IsInsulated(args.User, ent.Owner) ||
            !_powerCell.HasActivatableCharge(ent.Owner, user: args.User))
            return;

        args.Cancelled = true;
        ShockHandler(ent.Owner, args.User);
    }

    // Touching someone shocks them unless they are insulated.
    [SubscribeLocalEvent]
    private void OnHandInteract(Entity<InventoryComponent> ent, ref UserInteractHandEvent args)
    {
        if (_net.IsServer && TryGetActiveGloves(ent.Owner, out var gloves))
            StartTouch(gloves, args.Target, ent.Owner);
    }

    // Punches also shock.
    [SubscribeLocalEvent]
    private void OnMeleeHit(Entity<CMODefibGlovesComponent> ent, ref MeleeHitEvent args)
    {
        if (!_net.IsServer || !args.IsHit || !IsWorn(ent.Owner, args.User) || !_toggle.IsActivated(ent.Owner))
            return;

        foreach (var target in args.HitEntities)
            StartTouch(ent.Owner, target, args.User);
    }

    // Trying to touch them in a strip menu shocks you.
    [SubscribeLocalEvent]
    private void OnStripAttempt(Entity<StrippableComponent> ent, ref StripAttemptEvent args)
    {
        if (!_net.IsServer || args.Target != ent.Owner || !TryGetActiveGloves(ent.Owner, out var gloves) ||
            args.Item != gloves || args.User.Owner == ent.Owner || IsInsulated(args.User.Owner, gloves))
            return;

        args.Cancel();
        StartTouch(gloves, args.User.Owner, ent.Owner);
    }

    private bool TryGetActiveGloves(EntityUid wearer, out EntityUid gloves)
    {
        gloves = default;
        if (!_inventory.TryGetSlotEntity(wearer, "gloves", out var item) || item is not { } uid ||
            !HasComp<CMODefibGlovesComponent>(uid) || !_toggle.IsActivated(uid))
            return false;

        gloves = uid;
        return true;
    }

    private bool IsWorn(EntityUid gloves, EntityUid wearer) =>
        _inventory.TryGetSlotEntity(wearer, "gloves", out var item) && item == gloves;

    private bool HasInsulatedGloves(EntityUid wearer) =>
        _inventory.TryGetSlotEntity(wearer, "gloves", out var item) &&
        item is { } gloves && TryComp<InsulatedComponent>(gloves, out var insulation) &&
        insulation.Coefficient <= 0f;

    private bool CanZap(EntityUid gloves, EntityUid target, EntityUid wearer)
    {
        return _toggle.IsActivated(gloves) && HasComp<MobStateComponent>(target) &&
                !IsInsulated(target, gloves) &&
                _powerCell.HasActivatableCharge(gloves, user: wearer, predicted: true);
    }

    private bool IsInsulated(EntityUid target, EntityUid source)
    {
        var attempt = new ElectrocutionAttemptEvent(target, source, 1f, ~SlotFlags.POCKET);
        RaiseLocalEvent(target, attempt, true);
        return attempt.Cancelled || attempt.SiemensCoefficient <= 0f;
    }

    private bool TryStartZap(EntityUid gloves, EntityUid target, EntityUid wearer)
    {
        if (!CanZap(gloves, target, wearer))
            return false;

        var defib = Comp<DefibrillatorComponent>(gloves);
        if (_mobState.IsAlive(target) || _mobState.IsCritical(target))
        {
            ZapLiving(gloves, defib, target, wearer);
            return true;
        }

        if (!_defib.CanZap(gloves, target, wearer))
            return false;

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, wearer, defib.DoAfterDuration,
            new CMODefibGlovesDoAfterEvent(), gloves, target, gloves)
        {
            NeedHand = false,
            BreakOnMove = !defib.AllowDoAfterMovement,
        }, out var doAfterId);

        if (!started)
            return false;

        _audio.PlayPvs(defib.ChargeSound, gloves);

        var readyDelay = defib.DoAfterDuration - Comp<CMODefibGlovesComponent>(gloves).ReadySoundLead;
        Timer.Spawn(readyDelay > TimeSpan.Zero ? readyDelay : TimeSpan.Zero, () =>
        {
            if (!_doAfter.IsRunning(doAfterId) || !Exists(gloves) || !Exists(target) || !Exists(wearer) ||
                !IsWorn(gloves, wearer) || !_toggle.IsActivated(gloves) || !_mobState.IsDead(target))
                return;

            _audio.PlayPvs(defib.ReadySound, gloves);
        });
        return true;
    }

    // Apply contact shocks only to another uninsulated entity.
    private void StartTouch(EntityUid gloves, EntityUid target, EntityUid wearer)
    {
        if (target != wearer && !IsInsulated(target, gloves))
            TryStartZap(gloves, target, wearer);
    }

    // Unsafe handling uses ordinary electrocution so the target drops held items.
    private void ShockHandler(EntityUid gloves, EntityUid target)
    {
        if (!_net.IsServer || !HasComp<MobStateComponent>(target) ||
            IsInsulated(target, gloves) ||
            !TryComp<DefibrillatorComponent>(gloves, out var defib) ||
            !_powerCell.TryUseActivatableCharge(gloves, user: target))
            return;

        _audio.PlayPvs(defib.ZapSound, gloves);
        _electrocution.TryDoElectrocution(target, gloves, defib.ZapDamage, defib.WritheDuration, true);
    }

    // Intentionally using them to shock living targets does not make the target drop items.
    private void ZapLiving(EntityUid gloves, DefibrillatorComponent defib, EntityUid target, EntityUid wearer)
    {
        if (!_powerCell.TryUseActivatableCharge(gloves, user: wearer))
            return;

        var selfEvent = new SelfBeforeDefibrillatorZapsEvent(wearer, gloves, target);
        RaiseLocalEvent(wearer, selfEvent);
        target = selfEvent.DefibTarget;
        if (selfEvent.Cancelled || !CanZapLivingTarget(gloves, target))
            return;

        var targetEvent = new TargetBeforeDefibrillatorZapsEvent(wearer, gloves, target);
        RaiseLocalEvent(target, targetEvent);
        target = targetEvent.DefibTarget;
        if (targetEvent.Cancelled || !CanZapLivingTarget(gloves, target))
            return;

        _audio.PlayPvs(defib.ZapSound, gloves);
        var knockdownDuration = Comp<CMODefibGlovesComponent>(gloves).KnockdownDuration;
        if (_electrocution.TryDoGloveElectrocution(target, gloves, defib.ZapDamage, defib.WritheDuration,
                knockdownDuration, true, ignoreInsulation: true))
            _audio.PlayPvs(Comp<CMODefibGlovesComponent>(gloves).TouchShockSound, target);

        _audio.PlayPvs(defib.FailureSound, gloves);

        if (!_powerCell.HasActivatableCharge(gloves))
            _toggle.TryDeactivate(gloves);

        var ev = new TargetDefibrillatedEvent(wearer, (gloves, defib));
        RaiseLocalEvent(target, ref ev);
    }

    private bool CanZapLivingTarget(EntityUid gloves, EntityUid target) =>
        (_mobState.IsAlive(target) || _mobState.IsCritical(target)) && !IsInsulated(target, gloves);
}
