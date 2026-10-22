using System.Linq;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Changeling;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    private static readonly ProtoId<TagPrototype> IpcTag = "IPC";

    private void InitializeColonyCapture()
    {
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ArmChangelingNeurotoxinEvent>(OnArmNeurotoxin);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, MeleeHitEvent>(OnNeurotoxinHit);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ChangelingDevourAttemptEvent>(OnPossessionAttempt);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ChangelingDevourStartedEvent>(OnPossessionStarted);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ChangelingDevourFinishedEvent>(OnPossessionFinished);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, DoAfterAttemptEvent<ChangelingDevourWindupDoAfterEvent>>(OnPossessionWindupAttempt);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent>>(
            OnPossessionConsumeAttempt, before: [typeof(ChangelingDevourSystem)]);
    }

    private bool IsPossessable(EntityUid target)
        => !TerminatingOrDeleted(target) && HasComp<BodyComponent>(target) && HasComp<BloodstreamComponent>(target)
           && !HasComp<ChangelingComponent>(target) && !HasComp<ChangelingColonyMemberComponent>(target)
           && !HasComp<AbsorbedComponent>(target) && !_tag.HasTag(target, IpcTag);

    private void OnArmNeurotoxin(EntityUid uid, ChangelingColonyMemberComponent member, ref ArmChangelingNeurotoxinEvent args)
    {
        if (args.Handled || !member.IsFragment || member.IsBlood || _timing.CurTime < member.NeuroReady
            || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || !IsHivePlayer(uid, colony))
            return;
        args.Handled = true;
        member.NeuroArmed = !member.NeuroArmed;
        _actions.SetToggled(member.NeuroAction, member.NeuroArmed);
    }

    private void OnNeurotoxinHit(EntityUid uid, ChangelingColonyMemberComponent member, MeleeHitEvent args)
    {
        if (!args.IsHit || args.User != uid || !member.NeuroArmed || _timing.CurTime < member.NeuroReady
            || member.CaptureTarget != null || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony)
            || !IsHivePlayer(uid, colony) || !TryComp<ChangelingDevourComponent>(uid, out var devour))
            return;
        foreach (var target in args.HitEntities)
        {
            if (!IsPossessable(target))
                continue;
            member.NeuroArmed = false;
            member.NeuroReady = _timing.CurTime + TimeSpan.FromMinutes(5);
            _actions.SetToggled(member.NeuroAction, false);
            _actions.SetCooldown(member.NeuroAction, _timing.CurTime, member.NeuroReady);
            member.EmpoweredCapture = true;
            _devour.StartDevour((uid, devour), target);
            if (member.CaptureTarget == null)
                member.EmpoweredCapture = false;
            break;
        }
    }

    private void OnPossessionAttempt(EntityUid uid, ChangelingColonyMemberComponent member, ref ChangelingDevourAttemptEvent args)
    {
        if (TryComp<ChangelingColonyMemberComponent>(args.Target, out var target) && target.Mind == member.Mind)
        {
            args.Cancelled = true;
            return;
        }
        if (!member.IsFragment)
            return;
        args.Cancelled |= !IsPossessable(args.Target) || member.CaptureTarget != null
            || HasComp<ChangelingCaptureComponent>(args.Target)
            || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || !IsHivePlayer(uid, colony);
    }

    private void OnPossessionStarted(EntityUid uid, ChangelingColonyMemberComponent member, ref ChangelingDevourStartedEvent args)
    {
        if (!member.IsFragment)
            return;
        member.CaptureTarget = args.Target;
        member.CaptureStarted = _timing.CurTime;
        EnsureComp<ChangelingCaptureComponent>(args.Target).Attacker = uid;
        if (member.EmpoweredCapture)
            _movementMod.TryAddMovementSpeedModDuration(args.Target, "ChangelingCaptureSlowdown", TimeSpan.FromSeconds(3), 0.65f);
    }

    private void OnPossessionWindupAttempt(EntityUid uid, ChangelingColonyMemberComponent member,
        ref DoAfterAttemptEvent<ChangelingDevourWindupDoAfterEvent> args)
    {
        if (!CanContinuePossession(uid, member, args.DoAfter.Args.Target))
            args.Cancel();
    }

    private void OnPossessionConsumeAttempt(EntityUid uid, ChangelingColonyMemberComponent member,
        ref DoAfterAttemptEvent<ChangelingDevourConsumeDoAfterEvent> args)
    {
        if (!CanContinuePossession(uid, member, args.DoAfter.Args.Target))
            args.Cancel();
    }

    private bool CanContinuePossession(EntityUid uid, ChangelingColonyMemberComponent member, EntityUid? target)
        => !member.IsFragment || target is { } victim && member.CaptureTarget == victim && IsPossessable(victim)
           && _mobState.IsAlive(uid) && TryComp<ChangelingColonyComponent>(member.Mind, out var colony) && IsHivePlayer(uid, colony);

    private void OnPossessionFinished(EntityUid uid, ChangelingColonyMemberComponent member, ref ChangelingDevourFinishedEvent args)
    {
        if (!member.IsFragment)
            return;
        args.Handled = true;
        var valid = member.CaptureTarget == args.Target && CanContinuePossession(uid, member, args.Target);
        EndCapture(uid, member);
        if (!args.Completed || !valid || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony))
            return;

        var target = args.Target;
        if (!_mind.TryGetMind(uid, out var attackerMind, out _))
            return;
        AbsorbHiveMind(uid, target);
        _damage.SetAllDamage(target, FixedPoint2.Zero);
        _blood.TryModifyBloodLevel(target, 1000);
        _mobState.ChangeMobState(target, MobState.Alive);
        AddColonyMember(target, member.Mind, colony);
        if (TransferColony(attackerMind, colony, target))
            QueueDel(uid);
    }

    private void EndCapture(EntityUid uid, ChangelingColonyMemberComponent member)
    {
        if (member.CaptureTarget is { } target && TryComp<ChangelingCaptureComponent>(target, out var capture)
            && capture.Attacker == uid)
        {
            _statusEffect.TryRemoveStatusEffect(target, "ChangelingCaptureSlowdown");
            _statusEffect.TryRemoveStatusEffect(target, "ChangelingCaptureParalysis");
            RemComp<ChangelingCaptureComponent>(target);
        }
        member.CaptureTarget = null;
        member.EmpoweredCapture = false;
    }

    private void UpdateColonyCaptures()
    {
        var query = EntityQueryEnumerator<ChangelingColonyMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.CaptureTarget is not { } target)
                continue;
            if (!CanContinuePossession(uid, member, target))
            {
                EndCapture(uid, member);
                continue;
            }
            var elapsed = _timing.CurTime - member.CaptureStarted;
            if (member.EmpoweredCapture && elapsed >= TimeSpan.FromSeconds(3))
            {
                // This dedicated status can be removed without clearing another source's stun.
                var devour = Comp<ChangelingDevourComponent>(uid);
                var remaining = devour.DevourWindupTime + devour.DevourConsumeTime - elapsed;
                if (remaining > TimeSpan.Zero)
                    _statusEffect.TryAddStatusEffectDuration(target, "ChangelingCaptureParalysis", remaining);
                member.EmpoweredCapture = false;
            }
        }
    }
}
