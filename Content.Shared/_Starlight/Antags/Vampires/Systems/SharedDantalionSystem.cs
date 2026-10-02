using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public sealed partial class SharedDantalionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedVampireActionUseSystem _vampireActions = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireDecoyActionEvent>(OnDecoy);
        SubscribeLocalEvent<VampireBloodBondActionEvent>(OnBloodBond);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var invisQuery = EntityQueryEnumerator<ActiveVampireInvisibilityComponent>();
        while (invisQuery.MoveNext(out var uid, out var invis))
        {
            if (now < invis.EndTime)
                continue;

            RemComp<ActiveVampireInvisibilityComponent>(uid);
            RestoreStealth(uid, invis);
        }
    }

    private void OnDecoy(VampireDecoyActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity)
            || !TryComp<DantalionComponent>(uid, out var dantalion)
            || !_vampireActions.TryUse(uid, actionEntity))
            return;

        Entity<DantalionComponent> ent = (uid, dantalion);
        var hadStealth = TryComp<StealthComponent>(uid, out var stealth);
        var previousEnabled = stealth?.Enabled ?? false;
        var previousVisibility = hadStealth ? _stealth.GetVisibility(uid, stealth) : 1f;

        stealth ??= EnsureComp<StealthComponent>(uid);
        _stealth.SetEnabled(uid, true, stealth);
        _stealth.SetVisibility(uid, args.DecoyVisibility, stealth);

        var invisDuration = args.InvisibilityDuration < TimeSpan.Zero ? TimeSpan.Zero : args.InvisibilityDuration;
        if (invisDuration > TimeSpan.Zero)
        {
            var decoyEv = new VampireDecoyActivatedEvent(
                ent,
                args,
                invisDuration,
                hadStealth,
                previousEnabled,
                previousVisibility);
            RaiseLocalEvent(uid, ref decoyEv, true);
        }
        else
        {
            RestoreStealth(uid, hadStealth, previousEnabled, previousVisibility);

            var decoyEv = new VampireDecoyActivatedEvent(
                ent,
                args,
                TimeSpan.Zero,
                hadStealth,
                previousEnabled,
                previousVisibility);
            RaiseLocalEvent(uid, ref decoyEv, true);
        }

        args.Handled = true;
    }

    private void OnBloodBond(VampireBloodBondActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity))
            return;

        if (!TryComp<DantalionComponent>(uid, out var dantalion))
            return;

        Entity<DantalionComponent> ent = (uid, dantalion);
        if (ent.Comp.BloodBondActive)
        {
            ent.Comp.BloodBondActive = false;
            Dirty(ent);
            _popup.PopupPredicted(Loc.GetString("vampire-blood-bond-stop"), uid, uid);
        }
        else
        {
            if (!_vampireActions.TryUse(uid, actionEntity))
                return;

            var attempt = new VampireBloodBondStartAttemptEvent(ent);
            RaiseLocalEvent(uid, ref attempt, true);
            if (attempt.Cancelled)
                return;

            ent.Comp.BloodBondActive = true;
            Dirty(ent);
            _popup.PopupPredicted(Loc.GetString("vampire-blood-bond-start"), uid, uid);
            var started = new VampireBloodBondStartedEvent(ent, args);
            RaiseLocalEvent(uid, ref started, true);
        }

        if (_actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), ent.Comp.BloodBondActive);

        args.Handled = true;

        if (!ent.Comp.BloodBondActive)
        {
            var stopped = new VampireBloodBondStoppedEvent(ent);
            RaiseLocalEvent(uid, ref stopped, true);
        }
    }

    private void RestoreStealth(EntityUid uid, ActiveVampireInvisibilityComponent invis)
        => RestoreStealth(uid, invis.HadStealthComponent, invis.PreviousStealthEnabled, invis.PreviousStealthVisibility);

    private void RestoreStealth(EntityUid uid, bool hadStealthComponent, bool previousStealthEnabled, float previousStealthVisibility)
    {
        if (!TryComp<StealthComponent>(uid, out var stealth))
            return;

        if (!hadStealthComponent)
        {
            RemComp<StealthComponent>(uid);
            return;
        }

        _stealth.SetEnabled(uid, previousStealthEnabled, stealth);
        _stealth.SetVisibility(uid, previousStealthVisibility, stealth);
    }

}
