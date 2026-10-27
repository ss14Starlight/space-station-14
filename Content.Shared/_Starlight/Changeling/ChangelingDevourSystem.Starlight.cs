using System.Linq;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Changeling.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Shared.Changeling.Systems;

public sealed partial class ChangelingDevourSystem
{
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    // Only the explicit marker enables these rules; the common component retains its ordinary defaults.
    private bool CanDevourLivingPrey(EntityUid user)
        => HasComp<StarlightChangelingDevourComponent>(user);

    private bool IsDevourablePreyType(EntityUid user, EntityUid target)
        => CanDevourLivingPrey(user) ? HasComp<MobStateComponent>(target) : HasComp<HumanoidAppearanceComponent>(target);

    private AttemptFrequency GetDevourAttemptFrequency(EntityUid user)
        => CanDevourLivingPrey(user) ? AttemptFrequency.EveryTick : AttemptFrequency.Never;

    private bool HasDevouredAnimal(EntityUid user, EntityUid target)
        => CanDevourLivingPrey(user)
            && TryComp<ChangelingDevouredComponent>(target, out var prey)
            && prey.DevouredBy.Contains(user);

    private void TrackDevouredAnimal(EntityUid user, EntityUid target)
    {
        if (!HasComp<HumanoidAppearanceComponent>(target)
            && TryComp<StarlightChangelingDevourComponent>(user, out var devour))
            devour.DevouredAnimals.Add(target);
    }

    private bool ApplyDevourCompletionDamage(Entity<ChangelingDevourComponent> ent, EntityUid target, TimeSpan duration)
    {
        if (TryComp<StarlightChangelingDevourComponent>(ent, out var devour))
            return TryFinishLivingDevour(ent, devour, target, duration);

        _damageable.ChangeDamage(target, ent.Comp.DevourDamage, true, true, ent.Owner);
        return true;
    }

    [SubscribeLocalEvent]
    private void OnStarlightDevourStartup(Entity<StarlightChangelingDevourComponent> ent, ref ComponentStartup args)
        => ConfigureStarlightDevour(ent.Owner);

    /// <summary>
    /// Grants the Starlight devour and restores it if another transformation removed
    /// the common component while leaving the opt-in component in place.
    /// </summary>
    /// <remarks>Safe to call repeatedly; an existing marker does not imply the common action is still present.</remarks>
    public void EnsureStarlightDevour(EntityUid uid)
    {
        if (_net.IsClient)
            return;

        EnsureComp<StarlightChangelingDevourComponent>(uid);
        ConfigureStarlightDevour(uid);
    }

    /// <summary>
    /// Configures the shared action and windup on the server. Consumption duration and the damage mix
    /// remain on the common component; MapInit grants the action if the entity has not reached that stage yet.
    /// </summary>
    private void ConfigureStarlightDevour(EntityUid uid)
    {
        // The client receives the component, action reference and timings from the server.
        // Startup can run before the action's own network state has been applied.
        if (_net.IsClient)
            return;

        if (!TryComp<ChangelingDevourComponent>(uid, out var common))
        {
            AddComp(uid, new ChangelingDevourComponent
            {
                ChangelingDevourAction = "ActionChangelingDevourStarlight",
                DevourWindupTime = TimeSpan.FromSeconds(10),
            });
            return;
        }

        if (common.ChangelingDevourAction?.Id != "ActionChangelingDevourStarlight")
        {
            _actionsSystem.RemoveAction(uid, common.ChangelingDevourActionEntity);
            common.ChangelingDevourActionEntity = null;
            common.ChangelingDevourAction = "ActionChangelingDevourStarlight";
        }

        common.DevourWindupTime = TimeSpan.FromSeconds(10);
        if (LifeStage(uid) >= EntityLifeStage.MapInitialized)
            _actionsSystem.AddAction(uid, ref common.ChangelingDevourActionEntity, common.ChangelingDevourAction);
        Dirty(uid, common);
    }

    [SubscribeLocalEvent]
    private void OnStarlightDevourShutdown(Entity<StarlightChangelingDevourComponent> ent, ref ComponentShutdown args)
        => StopStarlightDevour(ent.Owner, ent.Comp);

    [SubscribeLocalEvent]
    private void OnStarlightIdentityRemove(Entity<ChangelingIdentityComponent> ent, ref ComponentRemove args)
        => StopStarlightDevour(ent);

    [SubscribeLocalEvent]
    private void OnStarlightPreyRemove(Entity<ChangelingDevouredComponent> ent, ref ComponentRemove args)
    {
        foreach (var owner in ent.Comp.DevouredBy)
        {
            if (TryComp<StarlightChangelingDevourComponent>(owner, out var devour))
                devour.DevouredAnimals.Remove(ent.Owner);
        }
    }

    private void CleanupDevouredAnimals(Entity<StarlightChangelingDevourComponent> ent)
    {
        // The identity system cleans humanoid links. Animals have no cloned identity, so release their links here.
        foreach (var target in ent.Comp.DevouredAnimals)
        {
            if (TryComp<ChangelingDevouredComponent>(target, out var devoured)
                && devoured.DevouredBy.Remove(ent.Owner))
                Dirty(target, devoured);
        }

        ent.Comp.DevouredAnimals.Clear();
    }

    /// <summary>
    /// Cancels feeding and releases prey references when the owning body loses its devour or identity components.
    /// </summary>
    private void StopStarlightDevour(EntityUid user, StarlightChangelingDevourComponent? devour = null)
    {
        // Losing owner-only state on a client must not cancel a server-owned devour.
        if (_net.IsClient || !Resolve(user, ref devour, false))
            return;

        if (TryComp<DoAfterComponent>(user, out var doAfters))
        {
            foreach (var active in doAfters.DoAfters.Values.ToArray())
            {
                if (active.Args.Event is ChangelingDevourWindupDoAfterEvent or ChangelingDevourConsumeDoAfterEvent)
                    _doAfterSystem.Cancel(active.Id);
            }
        }

        ResetStarlightDevour(user, devour);
        CleanupDevouredAnimals((user, devour));
    }

    /// <summary>
    /// Reserves one target for both do-afters so overlapping attempts cannot share damage progress.
    /// </summary>
    private bool TryBeginStarlightDevour(EntityUid user, EntityUid target)
    {
        if (!TryComp<StarlightChangelingDevourComponent>(user, out var devour))
            return true;
        if (devour.CurrentTarget != null)
            return false;

        devour.CurrentTarget = target;
        devour.DamageProgress = 0;
        Dirty(user, devour);
        return true;
    }

    /// <summary>
    /// Releases the current attempt after completion, cancellation or failed startup, preserving past prey records.
    /// </summary>
    private void ResetStarlightDevour(EntityUid user, StarlightChangelingDevourComponent? devour = null)
    {
        if (!Resolve(user, ref devour, false))
            return;

        devour.CurrentTarget = null;
        devour.DamageProgress = 0;
        Dirty(user, devour);
        if (TryComp<ChangelingDevourComponent>(user, out var common))
            common.CurrentDevourSound = _audio.Stop(common.CurrentDevourSound);
    }

    [SubscribeLocalEvent]
    private void OnStarlightDevourConsumeAttempt(Entity<StarlightChangelingDevourComponent> ent,
        ref DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent> args)
    {
        if (_net.IsClient || args.Cancelled || args.DoAfter.Args.Delay <= TimeSpan.Zero
            || args.DoAfter.Args.Target is not { } target || _mobState.IsDead(target)
            || !TryComp<ChangelingDevourComponent>(ent, out var common))
            return;

        // EveryTick keeps one continuous do-after; whole seconds plus DamageProgress prevent repeated damage.
        var seconds = Math.Floor((_timing.CurTime - args.DoAfter.StartTime).TotalSeconds);
        var progress = (float) Math.Clamp(seconds / args.DoAfter.Args.Delay.TotalSeconds, 0, 1);
        ApplyLivingDevourDamage((ent, common), ent.Comp, target, progress, args.DoAfter.Args.Delay);
    }

    /// <summary>
    /// Applies only newly reached progress, preserving the configured damage mix. The dead threshold
    /// represents maximum health; without one, the configured total is spread over the consumption duration.
    /// </summary>
    /// <param name="ent">The owner and shared damage configuration.</param>
    /// <param name="devour">Tracks the fraction already processed by earlier attempts.</param>
    /// <param name="target">The prey whose death threshold determines the damage scale.</param>
    /// <param name="progress">Cumulative consumption progress from zero to one.</param>
    /// <param name="duration">Consumption time used to convert progress into elapsed seconds.</param>
    private void ApplyLivingDevourDamage(Entity<ChangelingDevourComponent> ent,
        StarlightChangelingDevourComponent devour, EntityUid target, float progress, TimeSpan duration)
    {
        if (_net.IsClient || progress <= devour.DamageProgress)
            return;

        var scale = progress - devour.DamageProgress;
        var configuredDamage = ent.Comp.DevourDamage.GetTotal();
        // Preserve the damage mix while dealing 10% of the victim's maximum health per second.
        if (configuredDamage > 0 && _mobThreshold.TryGetDeadThreshold(target, out var deathThreshold))
            scale *= (float) deathThreshold.Value * 0.1f
                * (float) duration.TotalSeconds / (float) configuredDamage;
        devour.DamageProgress = progress;
        _damageable.ChangeDamage(target, ent.Comp.DevourDamage * scale, true, true, ent.Owner);
    }

    /// <summary>
    /// Applies remaining progress, then attempts to cross the death threshold with unmodified damage if needed.
    /// The server requires actual death before completion; the client leaves that decision to server state.
    /// </summary>
    private bool TryFinishLivingDevour(Entity<ChangelingDevourComponent> ent,
        StarlightChangelingDevourComponent devour, EntityUid target, TimeSpan duration)
    {
        ApplyLivingDevourDamage(ent, devour, target, 1, duration);
        var configuredDamage = ent.Comp.DevourDamage.GetTotal();
        if (_net.IsServer && !_mobState.IsDead(target) && configuredDamage > 0
            && _mobThreshold.TryGetDeadThreshold(target, out var deathThreshold))
        {
            var remaining = FixedPoint2.Max(FixedPoint2.Zero, deathThreshold.Value - _damageable.GetTotalDamage(target)) + 1;
            var damage = ent.Comp.DevourDamage * ((float) remaining / (float) configuredDamage);
            _damageable.ChangeDamage(target, damage, ignoreResistances: true, origin: ent.Owner, ignoreGlobalModifiers: true);
        }

        if (_net.IsClient || _mobState.IsDead(target))
            return true;

        _popupSystem.PopupClient(Loc.GetString("changeling-devour-attempt-failed-finish"), ent, ent, PopupType.Medium);
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent.Owner):player} unsuccessfully devoured {ToPrettyString(target):player}'s identity");
        return false;
    }
}
