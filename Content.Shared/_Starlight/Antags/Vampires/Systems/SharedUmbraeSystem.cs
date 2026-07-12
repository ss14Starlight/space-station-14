using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public sealed partial class SharedUmbraeSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVampireActionUseSystem _vampireActions = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireCloakOfDarknessActionEvent>(OnCloakOfDarkness);
        SubscribeLocalEvent<VampireShadowBoxingActionEvent>(OnShadowBoxing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<UmbraeComponent, TransformComponent, StealthComponent>();
        while (query.MoveNext(out var uid, out var umbrae, out var xform, out var stealth))
        {
            if (!umbrae.CloakOfDarknessActive)
                continue;

            if (now < umbrae.NextCloakOfDarknessVisibilityUpdate)
                continue;

            umbrae.NextCloakOfDarknessVisibilityUpdate = now + umbrae.CloakOfDarknessVisibilityUpdateInterval;
            Dirty(uid, umbrae);

            var visibility = GetCloakOfDarknessVisibility(uid, xform, umbrae);
            _stealth.SetVisibility(uid, visibility, stealth);
        }

        var shadowBoxingQuery = EntityQueryEnumerator<ActiveVampireShadowBoxingComponent, UmbraeComponent>();
        while (shadowBoxingQuery.MoveNext(out var uid, out var active, out var umbrae))
        {
            if (now < active.EndTime && umbrae.ShadowBoxingActive)
                continue;

            StopShadowBoxing(uid, umbrae, "action-vampire-shadow-boxing-ends");
        }
    }

    private void OnCloakOfDarkness(VampireCloakOfDarknessActionEvent args)
    {
        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (args.Handled
            || !Exists(actionEntity)
            || !_vampireActions.TryUse(uid, actionEntity))
        {
            return;
        }

        var umbrae = EnsureComp<UmbraeComponent>(uid);
        if (umbrae.CloakOfDarknessActive)
        {
            DeactivateCloakOfDarkness(uid, umbrae);
            _popup.PopupPredicted(Loc.GetString("action-vampire-cloak-of-darkness-stop"), uid, uid);
        }
        else
        {
            ActivateCloakOfDarkness(uid, umbrae);
            _popup.PopupPredicted(Loc.GetString("action-vampire-cloak-of-darkness-start"), uid, uid);
        }

        if (_actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), umbrae.CloakOfDarknessActive);

        args.Handled = true;
    }

    public void ActivateCloakOfDarkness(EntityUid uid, UmbraeComponent comp)
    {
        comp.CloakOfDarknessActive = true;
        comp.NextCloakOfDarknessVisibilityUpdate = _timing.CurTime;

        comp.CloakHadStealthComponent = TryComp<StealthComponent>(uid, out var existingStealth);
        comp.CloakPreviousStealthEnabled = existingStealth?.Enabled ?? false;
        comp.CloakPreviousStealthVisibility = comp.CloakHadStealthComponent ? _stealth.GetVisibility(uid, existingStealth) : 1f;
        Dirty(uid, comp);

        var stealth = existingStealth ?? EnsureComp<StealthComponent>(uid);
        _stealth.SetEnabled(uid, true, stealth);
        _stealth.SetVisibility(uid, comp.CloakOfDarknessMinVisibility, stealth);
    }

    public void DeactivateCloakOfDarkness(EntityUid uid, UmbraeComponent comp)
    {
        comp.CloakOfDarknessActive = false;
        Dirty(uid, comp);

        RestoreCloakStealth(uid, comp);
    }

    private void RestoreCloakStealth(EntityUid uid, UmbraeComponent comp)
    {
        if (!TryComp<StealthComponent>(uid, out var stealth))
            return;

        if (!comp.CloakHadStealthComponent)
        {
            RemComp<StealthComponent>(uid);
            return;
        }

        _stealth.SetEnabled(uid, comp.CloakPreviousStealthEnabled, stealth);
        _stealth.SetVisibility(uid, comp.CloakPreviousStealthVisibility, stealth);
    }

    private float GetCloakOfDarknessVisibility(EntityUid uid, TransformComponent xform, UmbraeComponent comp)
    {
        var range = comp.CloakOfDarknessRevealRange;
        if (range <= 0f)
            return comp.CloakOfDarknessMinVisibility;

        var center = _transform.GetWorldPosition(xform);
        var closest = range;

        foreach (var ent in _lookup.GetEntitiesInRange(xform.Coordinates, range))
        {
            if (ent == uid)
                continue;

            if (!HasComp<HumanoidAppearanceComponent>(ent) || HasComp<VampireComponent>(ent))
                continue;

            if (TryComp<MobStateComponent>(ent, out var mob)
                && mob.CurrentState == MobState.Dead)
                continue;

            var dist = (_transform.GetWorldPosition(Transform(ent)) - center).Length();
            closest = MathF.Min(closest, dist);
        }

        var t = 1f - Math.Clamp(closest / range, 0f, 1f);
        return MathHelper.Lerp(comp.CloakOfDarknessMinVisibility, comp.CloakOfDarknessMaxVisibility, t);
    }

    private void OnShadowBoxing(VampireShadowBoxingActionEvent args)
    {
        var uid = args.Performer;
        var actionEntity = args.Action.Owner;
        if (args.Handled
            || !Exists(actionEntity))
        {
            return;
        }

        if (TryComp<UmbraeComponent>(uid, out var umbrae) && umbrae.ShadowBoxingActive)
        {
            StopShadowBoxing(uid, umbrae, "action-vampire-shadow-boxing-ends");
            args.Handled = true;
            return;
        }

        var target = args.Target;
        if (target == uid
            || !Exists(target)
            || !HasComp<HumanoidAppearanceComponent>(target)
            || !TryComp<DamageableComponent>(target, out _)
            || (TryComp<MobStateComponent>(target, out var mob) && mob.CurrentState == MobState.Dead)
            || !_vampireActions.TryUse(uid, actionEntity))
        {
            return;
        }

        var attempt = new VampireShadowBoxingStartAttemptEvent(uid, target);
        RaiseLocalEvent(uid, ref attempt, true);
        if (attempt.Cancelled)
            return;

        umbrae = EnsureComp<UmbraeComponent>(uid);
        var now = _timing.CurTime;
        umbrae.ShadowBoxingActive = true;
        umbrae.ShadowBoxingEndTime = now + args.Duration;
        umbrae.ShadowBoxingTarget = target;
        umbrae.ShadowBoxingLoopRunning = true;
        Dirty(uid, umbrae);

        var active = EnsureComp<ActiveVampireShadowBoxingComponent>(uid);
        active.Target = target;
        active.Range = args.Range;
        active.BrutePerTick = args.BrutePerTick;
        active.HitSound = args.HitSound;
        active.PunchEffectPrototype = args.PunchEffectPrototype;
        active.TickInterval = args.Interval;
        active.NextTick = now + args.Interval;
        active.EndTime = now + args.Duration;

        _popup.PopupPredicted(Loc.GetString("action-vampire-shadow-boxing-start"), uid, uid);
        args.Handled = true;
    }

    public void StopShadowBoxing(EntityUid uid, UmbraeComponent umbrae, string popup)
    {
        umbrae.ShadowBoxingActive = false;
        umbrae.ShadowBoxingTarget = null;
        umbrae.ShadowBoxingEndTime = null;
        umbrae.ShadowBoxingLoopRunning = false;
        RemComp<ActiveVampireShadowBoxingComponent>(uid);
        Dirty(uid, umbrae);
        _popup.PopupPredicted(Loc.GetString(popup), uid, uid);
    }
}
