using Content.Shared.Atmos.Rotting;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Electrocution;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Timing;
using Content.Shared.Traits.Assorted;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Shared.Medical;

/// <summary>
/// Handles interactions and logic relating to <see cref="WearableDefibrillatorComponent"/>
/// </summary>
public abstract class SharedWearableDefibrillatorSystem : EntitySystem
{
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedRottingSystem _rotting = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

    private readonly HashSet<EntityUid> _interacters = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WearableDefibrillatorComponent, DefibrillatorZapDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<WearableDefibrillatorComponent, DefibActionEvent>(OnDefibAction);
        SubscribeLocalEvent<WearableDefibrillatorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<WearableDefibrillatorComponent, GotUnequippedEvent>(OnUnequipped);
    }

    /// <summary>
    /// Adds the defib action to the user.
    /// </summary>
    private void OnGetActions(Entity<WearableDefibrillatorComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags?.HasFlag(ent.Comp.RequiredSlot) != true)
            return;

        args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    /// <summary>
    /// Handles the doafter logic and then triggers the defib.
    /// </summary>
    private void OnDoAfter(Entity<WearableDefibrillatorComponent> ent, ref DefibrillatorZapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Target is not { } target)
            return;

        if (!CanDefib(ent, target, args.User))
            return;

        args.Handled = true;
        Defib(ent, target, args.User);
    }

    /// <summary>
    /// Cancels an active defib doafter if the defib is unequipped midway through.
    /// </summary>
    private void OnUnequipped(Entity<WearableDefibrillatorComponent> ent, ref GotUnequippedEvent args)
    {
        if (!args.SlotFlags.HasFlag(ent.Comp.RequiredSlot))
            return;

        if (!TryComp<DoAfterComponent>(args.Equipee, out var doAfterComp))
            return;

        foreach (var doAfter in doAfterComp.DoAfters.Values)
            if (doAfter.Args.Event is DefibrillatorZapDoAfterEvent)
                _doAfter.Cancel(args.Equipee, doAfter.Index);
    }

    /// <summary>
    /// Checks if the target is valid to be defibrillated.
    /// </summary>
    public bool CanDefib(Entity<WearableDefibrillatorComponent> ent, EntityUid target, EntityUid? user = null, bool targetCanBeAlive = false)
    {
        if (!TryComp<UseDelayComponent>(ent, out var useDelay) || _useDelay.IsDelayed((ent.Owner, useDelay), ent.Comp.DelayId))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return false;

        if (!targetCanBeAlive && _mobState.IsAlive(target, mobState))
            return false;

        if (!targetCanBeAlive && !ent.Comp.CanDefibCrit && _mobState.IsCritical(target, mobState))
            return false;

        return true;
    }

    /// <summary>
    /// Tries to start the defib when the action is used on someone.
    /// </summary>
    private void OnDefibAction(Entity<WearableDefibrillatorComponent> ent, ref DefibActionEvent args)
    {
        var user = args.Performer;
        var target = args.Target;

        if (!CanDefib(ent, target, user))
            return;

        _audio.PlayPvs(ent.Comp.ChargeSound, ent.Owner);
        _doAfter.TryStartDoAfter(
            new DoAfterArgs(EntityManager, user, ent.Comp.DoAfterDuration, new DefibrillatorZapDoAfterEvent(),
            ent.Owner, target, ent.Owner)
            {
                BreakOnMove = !ent.Comp.AllowDoAfterMovement
            });
    }

    /// <summary>
    /// Tries to defibrillate the target with the given defibrillator.
    /// </summary>
    /// <param name="ent">The defbrillator being used.</param>
    /// <param name="target">Uid of the target getting defibbed.</param>
    /// <param name="user">Uid of the entity using the defibrillator.</param>
    public void Defib(Entity<WearableDefibrillatorComponent> ent, EntityUid target, EntityUid user)
    {
        // checks if the defib target should be reversed (clumsy trait)
        var selfEvent = new SelfBeforeDefibrillatorZapsEvent(user, ent.Owner, target);
        RaiseLocalEvent(user, selfEvent);

        target = selfEvent.DefibTarget;

        // Ensure thet new target is still valid.
        if (selfEvent.Cancelled || !CanDefib(ent, target, user, true))
            return;

        var targetEvent = new TargetBeforeDefibrillatorZapsEvent(user, ent.Owner, target);
        RaiseLocalEvent(target, targetEvent);

        target = targetEvent.DefibTarget;

        // Check that the target is valid again.
        if (targetEvent.Cancelled || !CanDefib(ent, target, user, true))
            return;

        if (!TryComp<MobStateComponent>(target, out var targetMobState))
            return;

        // Play sound and shock the target.
        _audio.PlayPvs(ent.Comp.ZapSound, ent.Owner);
        _electrocution.TryDoElectrocution(target, ent.Owner, ent.Comp.ZapDamage, ent.Comp.WritheDuration, true, ignoreInsulation: true);

        // if anyone is still interacting with the target, shock them too.
        _interactionSystem.GetEntitiesInteractingWithTarget(target, _interacters);
        foreach (var other in _interacters)
        {
            if (other == user)
                continue;

            _electrocution.TryDoElectrocution(other, null, ent.Comp.ZapDamage, ent.Comp.WritheDuration, true);
        }

        // Start the delay on the defib.
        if (TryComp<UseDelayComponent>(ent, out var useDelay))
        {
            _useDelay.SetLength((ent.Owner, useDelay), ent.Comp.ZapDelay, id: ent.Comp.DelayId);
            _useDelay.TryResetDelay((ent.Owner, useDelay), id: ent.Comp.DelayId);
        }

        // Check if they are rotted.
        var failedRevive = true;
        if (_rotting.IsRotten(target))
        {
            _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("defibrillator-rotten"),
                InGameICChatType.Speak, true);
        }
        // Check if they have the unrevivable component.
        else if (TryComp<UnrevivableComponent>(target, out var unrevivable))
        {
            _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString(unrevivable.ReasonMessage),
                InGameICChatType.Speak, true);
        }
        else
        {
            // Heal them for the amount specefied in ZapHeal.
            if (_mobState.IsDead(target, targetMobState))
                _damageable.TryChangeDamage(target, ent.Comp.ZapHeal, true, origin: user);

            // Change their state to critical if they are above the death threshold.
            if (TryComp<MobThresholdsComponent>(target, out var targetThresholds) &&
                TryComp<DamageableComponent>(target, out var targetDamageable) &&
                _mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold, targetThresholds) &&
                targetDamageable.TotalDamage < threshold)
            {
                _mobState.ChangeMobState(target, MobState.Critical, targetMobState, user);
                failedRevive = false;
            }

            if (_mind.TryGetMind(target, out var mindUid, out var mindComp) &&
                _player.TryGetSessionById(mindComp.UserId, out var playerSession))
            {
                // Notify them they're being revived.
                if (mindComp.CurrentEntity != target)
                    OpenReturnToBodyEui((mindUid, mindComp), playerSession);
            }
            else
            {
                _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("defibrillator-no-mind"),
                    InGameICChatType.Speak, true);
            }
        }

        var sound = failedRevive
            ? ent.Comp.FailureSound
            : ent.Comp.SuccessSound;
        _audio.PlayPvs(sound, ent.Owner);
    }

    protected virtual void OpenReturnToBodyEui(Entity<MindComponent> mind, ICommonSession session) { }
}
