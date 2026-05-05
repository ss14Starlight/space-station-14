using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public abstract class SharedDantalionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DantalionComponent, VampireDecoyActionEvent>(OnDecoy);
        SubscribeLocalEvent<DantalionComponent, VampireBloodBondActionEvent>(OnBloodBond);
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

    protected virtual bool TryUseVampireAction(EntityUid uid, EntityUid actionEntity)
        => true;

    protected virtual bool CanStartBloodBond(Entity<DantalionComponent> ent)
        => true;

    protected virtual void OnPredictedDecoy(Entity<DantalionComponent> ent, VampireDecoyActionEvent args)
    {
    }

    protected virtual void OnPredictedBloodBondStarted(Entity<DantalionComponent> ent, VampireBloodBondActionEvent args)
    {
    }

    protected virtual void OnPredictedBloodBondStopped(Entity<DantalionComponent> ent)
    {
    }

    private void OnDecoy(Entity<DantalionComponent> ent, ref VampireDecoyActionEvent args)
    {
        if (args.Handled)
            return;

        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity) || !TryUseVampireAction(ent, actionEntity))
            return;

        var hadStealth = TryComp<StealthComponent>(ent, out var stealth);
        var previousEnabled = stealth?.Enabled ?? false;
        var previousVisibility = hadStealth ? _stealth.GetVisibility(ent, stealth) : 1f;

        stealth ??= EnsureComp<StealthComponent>(ent);
        _stealth.SetEnabled(ent, true, stealth);
        _stealth.SetVisibility(ent, -1f, stealth);

        var invisDuration = args.InvisibilityDuration < TimeSpan.Zero ? TimeSpan.Zero : args.InvisibilityDuration;
        if (invisDuration > TimeSpan.Zero)
        {
            var active = EnsureComp<ActiveVampireInvisibilityComponent>(ent);
            active.EndTime = _timing.CurTime + invisDuration;
            active.HadStealthComponent = hadStealth;
            active.PreviousStealthEnabled = previousEnabled;
            active.PreviousStealthVisibility = previousVisibility;
        }
        else
        {
            RestoreStealth(ent, hadStealth, previousEnabled, previousVisibility);
        }

        OnPredictedDecoy(ent, args);
        args.Handled = true;
    }

    private void OnBloodBond(Entity<DantalionComponent> ent, ref VampireBloodBondActionEvent args)
    {
        if (args.Handled)
            return;

        var uid = ent.Owner;
        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity))
            return;

        if (ent.Comp.BloodBondActive)
        {
            ent.Comp.BloodBondActive = false;
            Dirty(ent);
            _popup.PopupPredicted(Loc.GetString("vampire-blood-bond-stop"), uid, uid);
            OnPredictedBloodBondStopped(ent);
        }
        else
        {
            if (!TryUseVampireAction(uid, actionEntity)
                || !CanStartBloodBond(ent))
            {
                return;
            }

            ent.Comp.BloodBondActive = true;
            Dirty(ent);
            _popup.PopupPredicted(Loc.GetString("vampire-blood-bond-start"), uid, uid);
            OnPredictedBloodBondStarted(ent, args);
        }

        if (_actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), ent.Comp.BloodBondActive);

        args.Handled = true;
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
