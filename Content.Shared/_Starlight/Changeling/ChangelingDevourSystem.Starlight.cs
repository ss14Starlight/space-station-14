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

    public void EnsureStarlightDevour(EntityUid uid)
    {
        if (_net.IsClient)
            return;

        // A newly added marker configures the common component in its startup handler.
        if (EnsureComp<StarlightChangelingDevourComponent>(uid, out _))
            ConfigureStarlightDevour(uid);
    }

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
        if (LifeStage(uid) >= EntityLifeStage.MapInitialized
            && (_actionsSystem.GetAction(common.ChangelingDevourActionEntity) is not { } action || action.Comp.AttachedEntity != uid))
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

    private void StopStarlightDevour(EntityUid user, StarlightChangelingDevourComponent? devour = null)
    {
        // Losing owner-only state on a client must not cancel a server-owned devour.
        if (_net.IsClient || !Resolve(user, ref devour, false))
            return;

        if (devour.DoAfterIndex is { } index)
        {
            if (_doAfterSystem.IsRunning(user, index))
                _doAfterSystem.Cancel(user, index);
            devour.DoAfterIndex = null;
            Dirty(user, devour);
        }

        if (TryComp<ChangelingDevourComponent>(user, out var common))
            common.CurrentDevourSound = _audio.Stop(common.CurrentDevourSound);
        CleanupDevouredAnimals((user, devour));
    }

    // Reject a repeated action without letting duplicate handling cancel the existing devour.
    private bool CanStartStarlightDevour(EntityUid user)
        => !TryComp<StarlightChangelingDevourComponent>(user, out var devour)
            || devour.DoAfterIndex is not { } index || !_doAfterSystem.IsRunning(user, index);

    private bool TryStartDevourDoAfter(Entity<ChangelingDevourComponent> ent, DoAfterArgs args)
    {
        if (TryComp<StarlightChangelingDevourComponent>(ent, out var devour))
            devour.DamageProgress = 0;

        if (!_doAfterSystem.TryStartDoAfter(args, out var id))
        {
            ent.Comp.CurrentDevourSound = _audio.Stop(ent.Comp.CurrentDevourSound);
            return false;
        }

        // Instant windup can already have started consumption. Do not overwrite that phase's ID.
        if (devour != null && _doAfterSystem.IsRunning(id))
        {
            devour.DoAfterIndex = id.Value.Index;
            Dirty(ent.Owner, devour);
        }
        return true;
    }

    partial void OnDevourDoAfterFinished(EntityUid user, DoAfterId id)
    {
        if (!TryComp<StarlightChangelingDevourComponent>(user, out var devour) || devour.DoAfterIndex != id.Index)
            return;

        devour.DoAfterIndex = null;
        Dirty(user, devour);
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
