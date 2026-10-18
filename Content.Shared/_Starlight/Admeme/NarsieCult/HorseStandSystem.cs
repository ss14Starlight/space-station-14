using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Actions;
using Content.Shared.Body.Systems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.Bed.Sleep;

namespace Content.Shared._Starlight.Admeme.NarsieCult;

public sealed partial class HorseStandSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SleepingSystem _sleeping = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HorseStandComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HorseStandComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HorseStandComponent, HorseStandActionEvent>(OnToggleAction);
        SubscribeLocalEvent<HorseStandComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<HorseStandComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<HorseStandComponent, HorseSleepActionEvent>(OnSleep);
        SubscribeLocalEvent<HorseStandComponent, HorseEyesActionEvent>(OnEyes);
        SubscribeLocalEvent<HorseStandComponent, HorseStunActionEvent>(OnStun);
        SubscribeLocalEvent<HorseStandComponent, HorseRestrainActionEvent>(OnRestrain);
    }

    private void OnMapInit(Entity<HorseStandComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        Dirty(ent);

        // Make sure everything matches the starting stage (e.g. a mapper set standing: true).
        ApplyStage(ent);
    }

    private void OnShutdown(Entity<HorseStandComponent> ent, ref ComponentShutdown args)
    {
        RemoveStandingActions(ent);
        _actions.RemoveAction(ent.Owner, ent.Comp.ToggleActionEntity);
    }

    private void OnToggleAction(Entity<HorseStandComponent> ent, ref HorseStandActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetStanding(ent, !ent.Comp.Standing);
    }

    private void OnMobStateChanged(Entity<HorseStandComponent> ent, ref MobStateChangedEvent args)
    {
        // A dead creature drops back to the normal stage, without the popup or sound.
        if (args.NewMobState == MobState.Dead)
            SetStanding(ent, false, showEffects: false);
    }

    private void OnRefreshSpeed(EntityUid uid, HorseStandComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.Standing)
            args.ModifySpeed(comp.StandingSpeedModifier);
    }

    /// <summary>
    /// Switches the creature to the given stage. Only entering the standing stage plays the sound and popup.
    /// </summary>
    public void SetStanding(Entity<HorseStandComponent> ent, bool standing, bool showEffects = true)
    {
        if (ent.Comp.Standing == standing)
            return;

        // Incapacitated creatures can't choose to stand (dying is handled separately above).
        if (standing && _mobState.IsIncapacitated(ent.Owner))
            return;

        ent.Comp.Standing = standing;
        Dirty(ent);

        ApplyStage(ent);

        if (standing && showEffects)
        {
            // Visible and audible to everyone nearby, shown over the creature itself.
            _popup.PopupPredicted(Loc.GetString(ent.Comp.StandPopup), ent.Owner, ent.Owner, PopupType.Medium);
            _audio.PlayPredicted(ent.Comp.StandSound, ent.Owner, ent.Owner);
        }
    }

    /// <summary>
    /// Brings sprite, speed, toggle state and the standing-only actions in line with <see cref="HorseStandComponent.Standing"/>.
    /// </summary>
    private void ApplyStage(Entity<HorseStandComponent> ent)
    {
        _appearance.SetData(ent.Owner, HorseStandVisuals.Standing, ent.Comp.Standing);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Standing);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);

        if (ent.Comp.Standing)
        {
            AddStandingActions(ent);
        }
        else
        {
            RemoveStandingActions(ent);

            // Let go of whoever was being dragged around.
            if (TryComp<PullerComponent>(ent.Owner, out var puller)
                && puller.Pulling is { } pulled
                && TryComp<PullableComponent>(pulled, out var pullable))
            {
                _pulling.TryStopPull(pulled, pullable, ent.Owner);
            }
        }
    }

    // Action entities are only created and deleted on the server. The list is networked to the client.
    private void AddStandingActions(Entity<HorseStandComponent> ent)
    {
        if (!_net.IsServer || ent.Comp.StandingActionEntities.Count > 0)
            return;

        foreach (var proto in ent.Comp.StandingActions)
        {
            if (_actions.AddAction(ent.Owner, proto.Id) is { } action)
                ent.Comp.StandingActionEntities.Add(action);
        }

        Dirty(ent);
    }

    private void RemoveStandingActions(Entity<HorseStandComponent> ent)
    {
        if (!_net.IsServer || ent.Comp.StandingActionEntities.Count == 0)
            return;

        foreach (var action in ent.Comp.StandingActionEntities)
        {
            _actions.RemoveAction(ent.Owner, action);
        }

        ent.Comp.StandingActionEntities.Clear();
        Dirty(ent);
    }

    /// <summary>
    /// Pulls the target's eyes out of their head and drops them on the ground.
    /// Server only: organ removal and its side effects (blindness, hidden eye sprite) are server-side.
    /// </summary>
    private void OnEyes(Entity<HorseStandComponent> ent, ref HorseEyesActionEvent args)
    {
        if (args.Handled || !ent.Comp.Standing || !_net.IsServer)
            return;

        var target = args.Target;
        if (!_body.TryGetOrgansWithComponent<OrganEyesComponent>(target, out var eyes))
        {
            _popup.PopupEntity(Loc.GetString("horse-stand-eyes-fail"), ent.Owner, ent.Owner);
            return;
        }

        var removedAny = false;
        foreach (var eye in eyes)
        {
            // Remember which body part the eyes sit in, the extraction event needs it.
            if (!_container.TryGetContainingContainer((eye.Owner, null, null), out var container))
                continue;

            if (!_body.RemoveOrgan(eye.Owner, eye.Comp2))
                continue;

            // Removing an organ doesn't blind anyone by itself. Surgery raises this event for that,
            // so it is raised here too (blindness, hidden eye sprite on the victim).
            var ev = new SurgeryOrganExtracted(target, container.Owner, eye.Owner);
            RaiseLocalEvent(eye.Owner, ref ev);

            // Make sure the eyes end up on the ground at the victim's feet.
            _transform.SetCoordinates(eye.Owner, Transform(target).Coordinates);
            removedAny = true;
        }

        if (!removedAny)
        {
            _popup.PopupEntity(Loc.GetString("horse-stand-eyes-fail"), ent.Owner, ent.Owner);
            return;
        }

        args.Handled = true;
        _audio.PlayPvs(ent.Comp.EyesSound, target);

        // What the victim sees over themselves, then what everyone else nearby sees.
        _popup.PopupEntity(Loc.GetString("horse-stand-eyes-victim"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(
            Loc.GetString("horse-stand-eyes-others", ("target", Identity.Name(target, EntityManager))),
            target,
            Filter.PvsExcept(target),
            true,
            PopupType.MediumCaution);
    }

    /// <summary>
    /// Stuns and knocks down the target.
    /// </summary>
    private void OnStun(Entity<HorseStandComponent> ent, ref HorseStunActionEvent args)
    {
        if (args.Handled || !ent.Comp.Standing)
            return;

        var target = args.Target;
        var stunned = _stun.TryUpdateStunDuration(target, ent.Comp.StunDuration);
        var knocked = _stun.TryKnockdown(target, ent.Comp.StunDuration, force: true);

        if (!stunned && !knocked)
        {
            _popup.PopupClient(Loc.GetString("horse-stand-stun-fail"), ent.Owner, ent.Owner);
            return;
        }

        args.Handled = true;
        _audio.PlayPredicted(ent.Comp.StunSound, target, ent.Owner);
        _popup.PopupPredicted(
            Loc.GetString("horse-stand-stun-self", ("target", Identity.Name(target, EntityManager, ent.Owner))),
            Loc.GetString("horse-stand-stun-others", ("user", Identity.Name(ent.Owner, EntityManager)), ("target", Identity.Name(target, EntityManager))),
            ent.Owner,
            ent.Owner,
            PopupType.MediumCaution);
    }
    /// <summary>
    /// Instantly puts the target to sleep.
    /// </summary>
    private void OnSleep(Entity<HorseStandComponent> ent, ref HorseSleepActionEvent args)
    {
        if (args.Handled || !ent.Comp.Standing)
            return;

        var target = args.Target;

        // Already asleep → fail quietly
        if (HasComp<SleepingComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("horse-stand-sleep-fail"), ent.Owner, ent.Owner);
            return;
        }

        // TrySleeping refuses dead / cancelled / otherwise ineligible targets.
        // Do not show horse-stand-sleep-fail here — that string means "already asleep".
        if (!_sleeping.TrySleeping(target))
            return;

        args.Handled = true;
        _audio.PlayPredicted(ent.Comp.SleepSound, target, ent.Owner);
        _popup.PopupPredicted(
            Loc.GetString("horse-stand-sleep-self", ("target", Identity.Name(target, EntityManager, ent.Owner))),
            Loc.GetString("horse-stand-sleep-others", ("user", Identity.Name(ent.Owner, EntityManager)), ("target", Identity.Name(target, EntityManager))),
            ent.Owner,
            ent.Owner,
            PopupType.MediumCaution);
    }
    /// <summary>
    /// Instantly cuffs the target with real cuffs: no do-after, but the normal uncuff and break-out rules apply.
    /// </summary>
    private void OnRestrain(Entity<HorseStandComponent> ent, ref HorseRestrainActionEvent args)
    {
        if (args.Handled || !ent.Comp.Standing)
            return;

        var target = args.Target;
        if (!HasComp<CuffableComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("horse-stand-restrain-fail"), ent.Owner, ent.Owner);
            return;
        }

        // Spawn the cuffs at the target so the in-range check inside TryAddNewCuffs passes.
        var cuffs = PredictedSpawnAtPosition(ent.Comp.RestraintPrototype, Transform(target).Coordinates);
        if (!_cuffable.TryAddNewCuffs(target, ent.Owner, cuffs))
        {
            // Already fully cuffed, or no free hands to cuff.
            PredictedQueueDel(cuffs);
            _popup.PopupClient(Loc.GetString("horse-stand-restrain-fail"), ent.Owner, ent.Owner);
            return;
        }

        args.Handled = true;
        _popup.PopupPredicted(
            Loc.GetString("horse-stand-restrain-self", ("target", Identity.Name(target, EntityManager, ent.Owner))),
            Loc.GetString("horse-stand-restrain-others", ("user", Identity.Name(ent.Owner, EntityManager)), ("target", Identity.Name(target, EntityManager))),
            ent.Owner,
            ent.Owner,
            PopupType.MediumCaution);
    }
}
