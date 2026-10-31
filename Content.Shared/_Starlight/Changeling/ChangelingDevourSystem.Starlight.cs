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

    // Only the explicit setting enables these rules; the common component retains its ordinary defaults.
    private bool CanDevourLivingPrey(EntityUid user)
        => TryComp<ChangelingDevourComponent>(user, out var devour) && devour.UseStarlightDevour;

    private bool IsDevourablePreyType(EntityUid user, EntityUid target)
        => CanDevourLivingPrey(user) ? HasComp<MobStateComponent>(target) : HasComp<HumanoidAppearanceComponent>(target);

    private AttemptFrequency GetDevourAttemptFrequency(EntityUid user)
        => CanDevourLivingPrey(user) ? AttemptFrequency.EveryTick : AttemptFrequency.Never;

    private bool HasDevouredAnimal(EntityUid user, EntityUid target)
        => CanDevourLivingPrey(user)
            && TryComp<ChangelingDevouredComponent>(target, out var prey)
            && prey.DevouredBy.Contains(user);

    private void TrackDevouredAnimal(Entity<ChangelingDevourComponent> ent, EntityUid target)
    {
        if (ent.Comp.UseStarlightDevour && !HasComp<HumanoidAppearanceComponent>(target))
            ent.Comp.DevouredAnimals.Add(target);
    }

    private bool ApplyDevourCompletionDamage(Entity<ChangelingDevourComponent> ent, EntityUid target, TimeSpan duration)
    {
        if (ent.Comp.UseStarlightDevour)
            return TryFinishLivingDevour(ent, target, duration);

        _damageable.ChangeDamage(target, ent.Comp.DevourDamage, true, true, ent.Owner);
        return true;
    }

    /// <summary>
    /// Configures the existing devour component when Starlight's role is granted at runtime.
    /// Prototypes provide the same settings directly for entities that spawn with the ability.
    /// </summary>
    public void EnsureStarlightDevour(EntityUid uid)
    {
        // The client receives the component, action reference and timings from the server.
        // Startup can run before the action's own network state has been applied.
        if (_net.IsClient)
            return;

        if (!TryComp<ChangelingDevourComponent>(uid, out var common))
        {
            AddComp(uid, new ChangelingDevourComponent
            {
                UseStarlightDevour = true,
                ChangelingDevourAction = "ActionChangelingDevourStarlight",
                DevourWindupTime = TimeSpan.FromSeconds(10),
            });
            return;
        }

        common.UseStarlightDevour = true;
        if (common.ChangelingDevourAction?.Id != "ActionChangelingDevourStarlight")
        {
            _actionsSystem.RemoveAction(uid, common.ChangelingDevourActionEntity);
            common.ChangelingDevourActionEntity = null;
            common.ChangelingDevourAction = "ActionChangelingDevourStarlight";
        }

        common.DevourWindupTime = TimeSpan.FromSeconds(10);
        if (LifeStage(uid) >= EntityLifeStage.MapInitialized
            && (_actionsSystem.GetAction(common.ChangelingDevourActionEntity) is not { } action || action.Comp.AttachedEntity != uid))
            _actionsSystem.AddAction(uid, ref common.ChangelingDevourActionEntity, common.ChangelingDevourAction);
        Dirty(uid, common);
    }

    [SubscribeLocalEvent]
    private void OnStarlightIdentityRemove(Entity<ChangelingIdentityComponent> ent, ref ComponentRemove args)
        => StopStarlightDevour(ent);

    [SubscribeLocalEvent]
    private void OnStarlightPreyRemove(Entity<ChangelingDevouredComponent> ent, ref ComponentRemove args)
    {
        foreach (var owner in ent.Comp.DevouredBy)
        {
            if (TryComp<ChangelingDevourComponent>(owner, out var devour))
                devour.DevouredAnimals.Remove(ent.Owner);
        }
    }

    private void CleanupDevouredAnimals(Entity<ChangelingDevourComponent> ent)
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

    private void StopStarlightDevour(EntityUid user, ChangelingDevourComponent? devour = null)
    {
        // Losing owner-only state on a client must not cancel a server-owned devour.
        if (_net.IsClient || !Resolve(user, ref devour, false) || !devour.UseStarlightDevour)
            return;

        if (devour.DoAfterIndex is { } index)
        {
            if (_doAfterSystem.IsRunning(user, index))
                _doAfterSystem.Cancel(user, index);
            devour.DoAfterIndex = null;
            Dirty(user, devour);
        }

        devour.CurrentDevourSound = _audio.Stop(devour.CurrentDevourSound);
        CleanupDevouredAnimals((user, devour));
    }

    // Reject a repeated action without letting duplicate handling cancel the existing devour.
    private bool CanStartStarlightDevour(Entity<ChangelingDevourComponent> ent)
        => !ent.Comp.UseStarlightDevour
            || ent.Comp.DoAfterIndex is not { } index || !_doAfterSystem.IsRunning(ent, index);

    private bool TryStartDevourDoAfter(Entity<ChangelingDevourComponent> ent, DoAfterArgs args)
    {
        if (ent.Comp.UseStarlightDevour)
            ent.Comp.DamageProgress = 0;

        if (!_doAfterSystem.TryStartDoAfter(args, out var id))
        {
            ent.Comp.CurrentDevourSound = _audio.Stop(ent.Comp.CurrentDevourSound);
            return false;
        }

        // Instant windup can already have started consumption. Do not overwrite that phase's ID.
        if (ent.Comp.UseStarlightDevour && _doAfterSystem.IsRunning(id))
        {
            ent.Comp.DoAfterIndex = id.Value.Index;
            Dirty(ent);
        }
        return true;
    }

    partial void OnDevourDoAfterFinished(EntityUid user, DoAfterId id)
    {
        if (!TryComp<ChangelingDevourComponent>(user, out var devour)
            || !devour.UseStarlightDevour || devour.DoAfterIndex != id.Index)
            return;

        devour.DoAfterIndex = null;
        Dirty(user, devour);
    }

    [SubscribeLocalEvent]
    private void OnStarlightDevourConsumeAttempt(Entity<ChangelingDevourComponent> ent,
        ref DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent> args)
    {
        if (_net.IsClient || !ent.Comp.UseStarlightDevour || args.Cancelled || args.DoAfter.Args.Delay <= TimeSpan.Zero
            || args.DoAfter.Args.Target is not { } target || _mobState.IsDead(target))
            return;

        // EveryTick keeps one continuous do-after; whole seconds plus DamageProgress prevent repeated damage.
        var seconds = Math.Floor((_timing.CurTime - args.DoAfter.StartTime).TotalSeconds);
        var progress = (float) Math.Clamp(seconds / args.DoAfter.Args.Delay.TotalSeconds, 0, 1);
        ApplyLivingDevourDamage(ent, target, progress, args.DoAfter.Args.Delay);
    }

    private void ApplyLivingDevourDamage(Entity<ChangelingDevourComponent> ent,
        EntityUid target, float progress, TimeSpan duration)
    {
        if (_net.IsClient || progress <= ent.Comp.DamageProgress)
            return;

        var scale = progress - ent.Comp.DamageProgress;
        var configuredDamage = ent.Comp.DevourDamage.GetTotal();
        // Preserve the damage mix while dealing 10% of the victim's maximum health per second.
        if (configuredDamage > 0 && _mobThreshold.TryGetDeadThreshold(target, out var deathThreshold))
            scale *= (float) deathThreshold.Value * 0.1f
                * (float) duration.TotalSeconds / (float) configuredDamage;
        ent.Comp.DamageProgress = progress;
        _damageable.ChangeDamage(target, ent.Comp.DevourDamage * scale, true, true, ent.Owner);
    }

    private bool TryFinishLivingDevour(Entity<ChangelingDevourComponent> ent, EntityUid target, TimeSpan duration)
    {
        ApplyLivingDevourDamage(ent, target, 1, duration);
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
