using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Armor;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Changeling.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Store.Components;
using Content.Shared.Tools.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Changeling.Systems;

public sealed partial class ChangelingDevourSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private SharedChangelingIdentitySystem _changelingIdentitySystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingDevourComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChangelingDevourComponent, ChangelingDevourActionEvent>(OnDevourAction);
        SubscribeLocalEvent<ChangelingDevourComponent, ChangelingDevourWindupDoAfterEvent>(OnDevourWindup);
        SubscribeLocalEvent<ChangelingDevourComponent, ChangelingDevourConsumeDoAfterEvent>(OnDevourConsume);
        SubscribeLocalEvent<ChangelingDevourComponent, DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent>>(OnDevourConsumeAttempt);
        SubscribeLocalEvent<ChangelingDevourComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<ChangelingDevourComponent> ent, ref MapInitEvent args)
    {
        _actionsSystem.AddAction(ent, ref ent.Comp.ChangelingDevourActionEntity, ent.Comp.ChangelingDevourAction);
    }

    private void OnShutdown(Entity<ChangelingDevourComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ChangelingDevourActionEntity != null)
        {
            _actionsSystem.RemoveAction(ent.Owner, ent.Comp.ChangelingDevourActionEntity);
        }
    }

    /// <summary>
    /// Checkes if the targets outerclothing is beyond a DamageCoefficientThreshold to protect them from being devoured.
    /// </summary>
    /// <param name="target">The Targeted entity</param>
    /// <param name="ent">Changelings Devour Component</param>
    /// <returns>Is the target Protected from the attack</returns>
    private bool IsTargetProtected(EntityUid target, Entity<ChangelingDevourComponent> ent)
    {
        var ev = new CoefficientQueryEvent(SlotFlags.OUTERCLOTHING);

        RaiseLocalEvent(target, ev);

        foreach (var compProtectiveDamageType in ent.Comp.ProtectiveDamageTypes)
        {
            if (!ev.DamageModifiers.Coefficients.TryGetValue(compProtectiveDamageType, out var coefficient))
                continue;
            if (coefficient < 1f - ent.Comp.DevourPreventionPercentageThreshold)
                return true;
        }

        return false;
    }

    // The action was used.
    // Start the first doafter for the windup.
    public void StartDevour(Entity<ChangelingDevourComponent> ent, EntityUid target)
    {
        var action = new ChangelingDevourActionEvent { Performer = ent.Owner, Target = target };
        OnDevourAction(ent, ref action);
    }

    private void OnDevourAction(Entity<ChangelingDevourComponent> ent, ref ChangelingDevourActionEvent args)
    {
        if (args.Handled
            || _whitelistSystem.IsWhitelistFailOrNull(ent.Comp.Whitelist, args.Target)
            || !HasComp<ChangelingIdentityComponent>(ent))
            return;

        args.Handled = true;
        var target = args.Target;

        if (target == ent.Owner || !HasComp<MobStateComponent>(target))
            return;

        if (HasComp<RottingComponent>(target))
        {
            _popupSystem.PopupClient(Loc.GetString("changeling-devour-attempt-failed-rotting"), args.Performer, args.Performer, PopupType.Medium);
            return;
        }

        if (IsTargetProtected(target, ent))
        {
            _popupSystem.PopupClient(Loc.GetString("changeling-devour-attempt-failed-protected"), ent, ent, PopupType.Medium);
            return;
        }

        var attempt = new ChangelingDevourAttemptEvent(target);
        RaiseLocalEvent(ent, ref attempt);
        if (attempt.Cancelled)
            return;

        var started = new ChangelingDevourStartedEvent(target);
        RaiseLocalEvent(ent, ref started);

        if (_net.IsServer)
        {
            ent.Comp.CurrentDevourSound = _audio.Stop(ent.Comp.CurrentDevourSound);
            var pvsSound = _audio.PlayPvs(ent.Comp.DevourWindupNoise, ent);
            if (pvsSound != null)
                ent.Comp.CurrentDevourSound = pvsSound.Value.Entity;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ent:player} started changeling devour windup against {target:player}");

        if (!_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.DevourWindupTime, new ChangelingDevourWindupDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.None,
        }))
        {
            FinishDevour(ent, target, false);
            return;
        }

        var selfMessage = Loc.GetString("changeling-devour-begin-windup-self", ("user", Identity.Entity(ent.Owner, EntityManager)));
        var othersMessage = Loc.GetString("changeling-devour-begin-windup-others", ("user", Identity.Entity(ent.Owner, EntityManager)));
        _popupSystem.PopupPredicted(
            selfMessage,
            othersMessage,
            args.Performer,
            args.Performer,
            PopupType.MediumCaution);
    }

    // First doafter finished.
    // Start the second doafter for the actual consumption and deal a small amount of damage.
    private void OnDevourWindup(Entity<ChangelingDevourComponent> ent, ref ChangelingDevourWindupDoAfterEvent args)
    {
        args.Handled = true;
        ent.Comp.CurrentDevourSound = _audio.Stop(ent.Comp.CurrentDevourSound);

        if (args.Cancelled)
        {
            FinishDevour(ent, args.Target, false);
            return;
        }

        if (args.Target is not { } target)
            return;

        _damageable.ChangeDamage(target, ent.Comp.WindupDamage, true, true, ent.Owner);

        var selfMessage = Loc.GetString("changeling-devour-begin-consume-self", ("user", Identity.Entity(ent.Owner, EntityManager)));
        var othersMessage = Loc.GetString("changeling-devour-begin-consume-others", ("user", Identity.Entity(ent.Owner, EntityManager)));
        _popupSystem.PopupPredicted(
            selfMessage,
            othersMessage,
            args.User,
            args.User,
            PopupType.LargeCaution);

        if (_net.IsServer)
        {
            var pvsSound = _audio.PlayPvs(ent.Comp.ConsumeNoise, ent);

            if (pvsSound != null)
                ent.Comp.CurrentDevourSound = pvsSound.Value.Entity;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent.Owner):player} began to devour {ToPrettyString(target):player}'s identity");

        ent.Comp.DevourDamageProgress = 0f;
        if (!_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager,
            ent,
            ent.Comp.DevourConsumeTime,
            new ChangelingDevourConsumeDoAfterEvent(),
            ent,
            target: target,
            used: ent)
        {
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.None,
        }))
            FinishDevour(ent, target, false);
    }

    private void OnDevourConsumeAttempt(Entity<ChangelingDevourComponent> ent,
        ref DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent> args)
    {
        if (_net.IsClient || args.Cancelled || args.DoAfter.Args.Delay <= TimeSpan.Zero
            || args.DoAfter.Args.Target is not { } target)
            return;

        // Preserve the existing damage timing for corpses.
        if (_mobState.IsDead(target))
            return;

        // Keep one continuous progress bar, with damage once per second after the windup.
        var seconds = Math.Floor((_timing.CurTime - args.DoAfter.StartTime).TotalSeconds);
        var progress = (float) Math.Clamp(seconds / args.DoAfter.Args.Delay.TotalSeconds, 0, 1);
        ApplyDevourDamage(ent, target, progress);
    }

    private void ApplyDevourDamage(Entity<ChangelingDevourComponent> ent, EntityUid target, float progress)
    {
        if (_net.IsClient || progress <= ent.Comp.DevourDamageProgress)
            return;

        var damage = ent.Comp.DevourDamage * (progress - ent.Comp.DevourDamageProgress);
        ent.Comp.DevourDamageProgress = progress;
        _damageable.ChangeDamage(target, damage, true, true, ent.Owner);
    }

    // Second doafter finished.
    // Finish the damage, kill a living victim and save its identity.
    private void OnDevourConsume(Entity<ChangelingDevourComponent> ent, ref ChangelingDevourConsumeDoAfterEvent args)
    {
        args.Handled = true;
        ent.Comp.CurrentDevourSound = _audio.Stop(ent.Comp.CurrentDevourSound);

        if (args.Cancelled)
        {
            FinishDevour(ent, args.Target, false);
            return;
        }

        if (args.Target is not { } target)
            return;

        ApplyDevourDamage(ent, target, 1f);

        if (_net.IsServer && !_mobState.IsDead(target)
            && _mobThreshold.TryGetDeadThreshold(target, out var deathThreshold))
        {
            // Use the victim's actual death threshold rather than a fixed lethal dose.
            var remaining = FixedPoint2.Max(FixedPoint2.Zero, deathThreshold.Value - _damageable.GetTotalDamage(target)) + 1;
            var damage = ent.Comp.DevourDamage * ((float) remaining / (float) ent.Comp.DevourDamage.GetTotal());
            _damageable.ChangeDamage(target, damage, ignoreResistances: true, origin: ent.Owner, ignoreGlobalModifiers: true);
        }

        if (_net.IsServer && !_mobState.IsDead(target))
        {
            _popupSystem.PopupClient(Loc.GetString("changeling-devour-attempt-failed-not-dead"), args.User, args.User, PopupType.Medium);
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent.Owner):player} unsuccessfully devoured {ToPrettyString(args.Target):player}'s identity");
            FinishDevour(ent, target, false);
            return;
        }

        if (FinishDevour(ent, target, true))
            return;

        var selfMessage = Loc.GetString("changeling-devour-consume-complete-self", ("user", Identity.Entity(args.User, EntityManager)));
        var othersMessage = Loc.GetString("changeling-devour-consume-complete-others", ("user", Identity.Entity(args.User, EntityManager)));
        _popupSystem.PopupPredicted(
            selfMessage,
            othersMessage,
            args.User,
            args.User,
            PopupType.LargeCaution);

        if (_mobState.IsDead(target)
            && HasComp<HumanoidAppearanceComponent>(target)
            && TryComp<ChangelingIdentityComponent>(args.User, out var identityStorage))
        {
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent.Owner):player} successfully devoured {ToPrettyString(target):player}'s identity");
            _changelingIdentitySystem.CloneToPausedMap((ent, identityStorage), target);

            if (_inventorySystem.TryGetSlotEntity(target, "jumpsuit", out var item)
                && TryComp<ToolRefinableComponent>(item, out var refinable)) // Starlight
                RipClothing(target, (item.Value, refinable)); // Starlight
        }

        #region Starlight ling devour.
        var consumeEv = new OnLingDevour(target);
        RaiseLocalEvent(ent, ref consumeEv);
        #endregion

        Dirty(ent);
    }

    private bool FinishDevour(EntityUid user, EntityUid? target, bool completed)
    {
        if (target is not { } victim)
            return false;
        var finished = new ChangelingDevourFinishedEvent(victim, completed);
        RaiseLocalEvent(user, ref finished);
        return finished.Handled;
    }

    // TODO: This should just be an API method in the butcher system
    private void RipClothing(EntityUid victim, Entity<ToolRefinableComponent> item) // Starlight
    {
        var spawnEntities = EntitySpawnCollection.GetSpawns(item.Comp.RefineResult, _robustRandom); // Starlight

        foreach (var proto in spawnEntities)
        {
            // TODO: once predictedRandom is in, make this a Coordinate offset of 0.25f from the victims position
            PredictedSpawnNextToOrDrop(proto, victim);
        }

        PredictedQueueDel(item.Owner);
    }
}

#region Starlight ling consume event
[ByRefEvent]
public struct OnLingDevour(EntityUid target)
{
    public EntityUid Consumed = target;
}
#endregion
