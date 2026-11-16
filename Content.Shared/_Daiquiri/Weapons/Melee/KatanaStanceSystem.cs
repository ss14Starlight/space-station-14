using Content.Shared.Actions.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Daiquiri.Weapons.Melee;

public sealed partial class KatanaStanceSystem : EntitySystem
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KatanaStanceComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<KatanaStanceComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<KatanaStanceComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<KatanaStanceComponent, ItemToggleActivateAttemptEvent>(OnToggleAttempt);
        SubscribeLocalEvent<KatanaStanceComponent, ItemToggleDeactivateAttemptEvent>(OnToggleAttempt);
        SubscribeLocalEvent<KatanaStanceComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);
        SubscribeLocalEvent<KatanaStanceComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<KatanaStanceComponent, AttemptMeleeEvent>(OnAttemptMelee);

        SubscribeLocalEvent<KatanaBlockingComponent, DisarmAttemptEvent>(OnDisarmAttempt);
        SubscribeLocalEvent<KatanaBlockingComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<KatanaBlockingComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    private void OnEquipped(Entity<KatanaStanceComponent> ent, ref GotEquippedHandEvent args)
    {
        if (_net.IsServer)
        {
            var blocking = EnsureComp<KatanaBlockingComponent>(args.User);
            blocking.Katana = ent.Owner;
        }

        UpdateReflect(args.User, ent);
        _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnUnequipped(Entity<KatanaStanceComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (_net.IsServer &&
            TryComp<KatanaBlockingComponent>(args.User, out var blocking) &&
            blocking.Katana == ent.Owner)
        {
            RemComp<KatanaBlockingComponent>(args.User);
            RemComp<ReflectComponent>(args.User);
        }

        _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void UpdateReflect(EntityUid user, Entity<KatanaStanceComponent> katana)
    {
        if (!_net.IsServer || !_hands.IsHolding(user, katana.Owner))
            return;

        var reflect = EnsureComp<ReflectComponent>(user);
        reflect.Reflects = katana.Comp.Reflects;
        reflect.SoundOnReflect = katana.Comp.ReflectSound;
        reflect.ReflectProb = _toggle.IsActivated(katana.Owner)
            ? katana.Comp.DefenseReflectChance
            : katana.Comp.AttackReflectChance;

        Dirty(user, reflect);
    }

    private void OnToggled(Entity<KatanaStanceComponent> ent, ref ItemToggledEvent args)
    {
        _useDelay.TryResetDelay(ent.Owner);
        _item.VisualsChanged(ent.Owner);

        if (args.User is { } user)
        {
            UpdateReflect(user, ent);
            _movement.RefreshMovementSpeedModifiers(user);
            return;
        }

        if (_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            _movement.RefreshMovementSpeedModifiers(container.Owner);
    }

    private void OnToggleAttempt(Entity<KatanaStanceComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!CanToggle(ent.Owner, args.User) || _useDelay.IsDelayed(ent.Owner))
            args.Cancelled = true;
    }

    private void OnToggleAttempt(Entity<KatanaStanceComponent> ent, ref ItemToggleDeactivateAttemptEvent args)
    {
        if (!CanToggle(ent.Owner, args.User) || _useDelay.IsDelayed(ent.Owner))
            args.Cancelled = true;
    }

    private bool CanToggle(EntityUid katana, EntityUid? user)
    {
        return user is { } holder && _hands.IsHolding(holder, katana);
    }

    private void OnRefreshSpeed(Entity<KatanaStanceComponent> ent, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        var defense = _toggle.IsActivated(ent.Owner);
        var walk = defense ? ent.Comp.DefenseWalkModifier : ent.Comp.AttackWalkModifier;
        var sprint = defense ? ent.Comp.DefenseSprintModifier : ent.Comp.AttackSprintModifier;

        args.Args.ModifySpeed(walk, sprint);
    }

    private void OnGetMeleeDamage(Entity<KatanaStanceComponent> ent, ref GetMeleeDamageEvent args)
    {
        var defense = _toggle.IsActivated(ent.Owner);
        var multiplier = defense ? ent.Comp.DefenseDamageMultiplier : ent.Comp.AttackDamageMultiplier;

        if (multiplier == 1f)
            return;

        args.Damage = args.Damage * multiplier;
    }

    private void OnAttemptMelee(Entity<KatanaStanceComponent> ent, ref AttemptMeleeEvent args)
    {
        if (ent.Comp.CanAttackInDefense || !_toggle.IsActivated(ent.Owner))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString(ent.Comp.AttackBlockedPopup);
    }

    private void OnDisarmAttempt(Entity<KatanaBlockingComponent> ent, ref DisarmAttemptEvent args)
    {
        if (ent.Comp.Katana is not { } katana ||
            !TryComp<KatanaStanceComponent>(katana, out var stance) ||
            !_toggle.IsActivated(katana))
        {
            return;
        }

        if (!_random.Prob(stance.DisarmBlockChance))
            return;

        args.Cancelled = true;
    }

    private void OnAttacked(Entity<KatanaBlockingComponent> ent, ref AttackedEvent args)
    {
        ent.Comp.PendingAttacker = args.User;
        ent.Comp.PendingTick = _timing.CurTick;
    }

    private void OnBeforeDamageChanged(Entity<KatanaBlockingComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.PendingAttacker is null || ent.Comp.PendingAttacker != args.Origin)
            return;

        ent.Comp.PendingAttacker = null;

        if (_timing.CurTick > ent.Comp.PendingTick)
            return;

        if (ent.Comp.Katana is not { } katana ||
            !TryComp<KatanaStanceComponent>(katana, out var stance))
        {
            return;
        }

        var blockChance = _toggle.IsActivated(katana) ? stance.DefenseBlockChance : stance.AttackBlockChance;

        if (!_random.Prob(blockChance))
            return;

        args.Cancelled = true;

        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString(stance.BlockedPopup), ent.Owner);
        _audio.PlayPvs(stance.BlockSound, ent.Owner);
    }
}