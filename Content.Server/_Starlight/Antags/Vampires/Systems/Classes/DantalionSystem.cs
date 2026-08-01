using System.Linq;
using Content.Server._Starlight.Antags.Vampires.Components;
using Content.Server.Bible.Components;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;
using Content.Shared._Starlight.Medical.Damage;
using Content.Shared.Bed.Sleep;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Flash;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Prototypes;
using Content.Server.EUI;
using Content.Server._Starlight.Language;
using Content.Server.Roles;

namespace Content.Server._Starlight.Antags.Vampires.Systems.Classes;

public sealed partial class DantalionSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> _bruteGroupId = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> _burnGroupId = "Burn";
    private static readonly ProtoId<DamageTypePrototype> _asphyxiationTypeId = "Asphyxiation";

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private Content.Shared.Mind.SharedMindSystem _mind = default!;
    [Dependency] private VampireSystem _vampire = default!;
    [Dependency] private Content.Server.Actions.ActionsSystem _actions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private Shared.Examine.ExamineSystemShared _examine = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private EuiManager _euiMan = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private RoleSystem _role = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DantalionComponent, VampireEnthrallActionEvent>(OnEnthrall);
        SubscribeLocalEvent<DantalionComponent, VampireEnthrallDoAfterEvent>(OnEnthrallDoAfter);
        SubscribeLocalEvent<VampireThrallComponent, ComponentShutdown>(OnThrallShutdown);

        SubscribeLocalEvent<DantalionComponent, ComponentInit>((uid, _, _) => _language.AddLanguage(uid, "Dantalion"));
        SubscribeLocalEvent<DantalionComponent, ComponentShutdown>(OnDantalionShutdown);

        SubscribeLocalEvent<DantalionComponent, VampirePacifyActionEvent>(OnPacify);
        SubscribeLocalEvent<DantalionComponent, VampireSubspaceSwapActionEvent>(OnSubspaceSwap);

        SubscribeLocalEvent<DantalionComponent, VampireRallyThrallsActionEvent>(OnRallyThralls);
        SubscribeLocalEvent<DantalionComponent, VampireMassHysteriaActionEvent>(OnMassHysteria);
        SubscribeLocalEvent<VampireDecoyActivatedEvent>(OnDecoyActivated);
        SubscribeLocalEvent<VampireBloodBondStartAttemptEvent>(OnBloodBondStartAttempt);
        SubscribeLocalEvent<VampireBloodBondStartedEvent>(OnBloodBondStarted);
        SubscribeLocalEvent<VampireBloodBondStoppedEvent>(OnBloodBondStopped);

        SubscribeLocalEvent<DantalionComponent, VampireBloodDrankEvent>(OnBloodDrank);

        SubscribeLocalEvent<DantalionComponent, DamageBeforeApplyEvent>(OnDantalionDamage);
        SubscribeLocalEvent<VampireThrallComponent, DamageBeforeApplyEvent>(OnThrallDamage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<VampireThrallComponent>();
        while (query.MoveNext(out var uid, out var thrall))
        {
            if (now < thrall.NextBreakFreeCheck)
                continue;

            thrall.NextBreakFreeCheck = now + thrall.BreakFreeCheckInterval;
            Dirty(uid, thrall);
            CheckThrallBreakFree((uid, thrall));
        }

        ProcessActiveBloodBonds(now);
        ProcessActiveHysteriaVision(now);
    }

    private void ProcessActiveHysteriaVision(TimeSpan now)
    {
        var query = EntityQueryEnumerator<HysteriaVisionComponent>();
        while (query.MoveNext(out var uid, out var hysteria))
        {
            if (now < hysteria.EndTime)
                continue;

            RemComp<HysteriaVisionComponent>(uid);
        }
    }

    private void CheckThrallBreakFree(Entity<VampireThrallComponent> ent)
    {
        var (uid, thrall) = ent;

        thrall.HolyWaterConsumed = _solution.GetTotalPrototypeQuantity(uid, thrall.HolyWaterReagentId);
        Dirty(uid, thrall);
        if (thrall.HolyWaterConsumed >= thrall.HolyWaterToBreakFree)
        {
            if (TryReleaseThrall(uid))
                _popup.PopupEntity(Loc.GetString("vampire-thrall-holy-water-freed"), uid, uid, PopupType.Medium);
            return;
        }

        if (!HasComp<MindShieldComponent>(uid))
            return;

        if (TryReleaseThrall(uid))
            _popup.PopupEntity(Loc.GetString("vampire-thrall-released"), uid, uid, PopupType.Medium);
    }

    private void OnBloodDrank(EntityUid uid, DantalionComponent dantalion, ref VampireBloodDrankEvent args)
    {
        if (!TryComp<VampireComponent>(uid, out var vampire) || vampire.TotalBlood < dantalion.HealBloodThreshold)
            return;

        HealDantalionThralls((uid, dantalion));
    }

    #region Enthrall

    /// <summary>
    /// Checks if a target can be enthralled and starts a do after if they can be
    /// </summary>
    private void OnEnthrall(EntityUid uid, DantalionComponent dantalion, ref VampireEnthrallActionEvent args)
    {
        if (args.Handled || !Exists(args.Target))
            return;

        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        var actionEntity = args.Action.Owner;
        if (!TryGetActionBloodCost(actionEntity, out var bloodCost))
            return;

        var target = args.Target;

        if (HasComp<BibleUserComponent>(target) && vampire.FullPower != true)
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!IsValidEnthrallTarget(uid, target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-enthrall-invalid"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-enthrall-invalid"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!HasThrallCapacity(vampire, dantalion))
        {
            _popup.PopupEntity(Loc.GetString("vampire-enthrall-limit"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (vampire.DrunkBlood < bloodCost)
        {
            _popup.PopupEntity(Loc.GetString("vampire-not-enough-blood"), uid, uid, PopupType.MediumCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, uid, args.ChannelTime, new VampireEnthrallDoAfterEvent { BloodCost = bloodCost }, uid, target)
        {
            DistanceThreshold = 2.5f,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            MovementThreshold = 0.1f,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("vampire-enthrall-start", ("target", Identity.Entity(target, EntityManager))), uid, uid);
    }

    /// <summary>
    /// double checks the target can be enthralled and subtract blood from vampire to then give the target the thrall comp, objective, mind comp, and hivemind along with triggering a pop-up to inform the player they have been enthralled
    /// </summary>
    private void OnEnthrallDoAfter(EntityUid uid, DantalionComponent dantalion, ref VampireEnthrallDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        var target = args.Target.Value;

        if (!IsValidEnthrallTarget(uid, target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-enthrall-invalid"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-enthrall-invalid"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!HasThrallCapacity(vampire, dantalion))
        {
            _popup.PopupEntity(Loc.GetString("vampire-enthrall-limit"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!_vampire.CheckAndConsumeBloodCost(uid, vampire, null, args.BloodCost))
            return;

        var thrallComp = EnsureComp<VampireThrallComponent>(target);
        thrallComp.Master = uid;
        Dirty(target, thrallComp);

        dantalion.Thralls.Add(target);
        dantalion.ThrallSlotsUsed++;

        TryAssignThrallObeyObjective(uid, target, thrallComp);

        _language.AddLanguage(target, "Dantalion");

        _popup.PopupEntity(Loc.GetString("vampire-enthrall-success", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        _popup.PopupEntity(Loc.GetString("vampire-enthrall-target"), target, target, PopupType.Medium);
        args.Handled = true;
    }

    /// <summary>
    /// Attempts to apply the thrall objective and gives them the pop-up if it has been applied
    /// </summary>
    private void TryAssignThrallObeyObjective(EntityUid master, EntityUid thrall, VampireThrallComponent thrallComp)
    {
        if (!_mind.TryGetMind(thrall, out var thrallMindId, out var thrallMind))
            return;

        _role.MindAddRole(thrallMindId, thrallComp.MindRoleId, thrallMind, true);
        _mind.TryAddObjective(thrallMindId, thrallMind, thrallComp.ObeyObjectiveId);

        //adds pop-up for target informing them they have been enthralled
        if (_player.TryGetSessionById(thrallMind.UserId, out var session))
            _euiMan.OpenEui(new VampireThrallEui(), session);
    }

    private void OnThrallShutdown(EntityUid uid, VampireThrallComponent component, ComponentShutdown args)
    {
        if (component.Master is not { } master || !TryComp(master, out DantalionComponent? dantalion)
            || !dantalion.Thralls.Remove(uid))
            return;

        dantalion.ThrallSlotsUsed = Math.Max(0, dantalion.ThrallSlotsUsed - 1);
    }

    private void OnDantalionShutdown(EntityUid uid, DantalionComponent component, ComponentShutdown args)
        => ReleaseAllThralls(uid, component);

    private void ReleaseAllThralls(EntityUid _, DantalionComponent component)
    {
        if (component.Thralls.Count == 0)
            return;

        foreach (var thrall in component.Thralls.ToArray())
            TryReleaseThrall(thrall);
    }

    /// <summary>
	///     Called if a thrall is to be released from their master. Removes the antag component, objectives, role, and hivemind from the player
	/// </summary>
    private bool TryReleaseThrall(EntityUid thrall)
    {
        if (!TryComp<VampireThrallComponent>(thrall, out var comp))
            return false;

        if (_mind.TryGetMind(thrall, out var mindId, out var mind))
        {
            //Remove objectives
            if (_mind.TryFindObjective((mindId, mind), comp.ObeyObjectiveId, out var Objective) && Objective != null)
                _mind.TryRemoveObjective(mindId, mind, Objective.Value);
            //Remove role
            _role.MindRemoveRole<VampireThrallComponent>(mindId);
        }

        if (comp.Master is { } master && TryComp(master, out DantalionComponent? dantalion))
        {
            dantalion.BloodBondLinkedThralls.Remove(thrall);

            if (TryComp<VampireBloodBondBeamComponent>(master, out var beamComp) &&
                beamComp.ActiveBeams.Remove(thrall, out var connection))
            {
                var removeEvent = new VampireBloodBondBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, beamComp.VisualPrototype);
                RaiseNetworkEvent(removeEvent);
            }
        }

        //Remove the component
        RemComp<VampireThrallComponent>(thrall);

        _language.RemoveLanguage(thrall, "Dantalion");

        //If everything worked, thrall gets stunned for a few seconds for them to register that something has changed and notice they are no longer a thrall
        var stunTime = comp.DeconvertStunDuration;
        _stun.TryUpdateParalyzeDuration(thrall, stunTime);

        return true;
    }

    /// <summary>
	///     Checks if the vampire has enough blood to perform the action they are trying to perform
	/// </summary>
    private bool TryGetActionBloodCost(EntityUid actionEntity, out int bloodCost)
    {
        bloodCost = 0;

        if (!Exists(actionEntity) || !TryComp<VampireActionComponent>(actionEntity, out var actionComp))
            return false;

        bloodCost = (int)Math.Max(actionComp.BloodCost, 0);
        return true;
    }

    private bool IsValidEnthrallTarget(EntityUid uid, EntityUid target)
    {
        if (!Exists(target) || target == uid)
            return false;

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState) || mobState.CurrentState == Shared.Mobs.MobState.Dead)
            return false;

        if (HasComp<VampireComponent>(target) || HasComp<VampireThrallComponent>(target))
            return false;

        return true;
    }

    private bool HasThrallCapacity(VampireComponent comp, DantalionComponent dantalion)
        => dantalion.ThrallSlotsUsed < GetThrallLimit(comp, dantalion);

    private int GetThrallLimit(VampireComponent comp, DantalionComponent dantalion)
    {
        var limit = dantalion.BaseThrallLimit;

        if (comp.TotalBlood >= dantalion.ThrallLevel2Blood)
            limit++;

        if (comp.TotalBlood >= dantalion.ThrallLevel3Blood)
            limit++;

        if (comp.FullPower)
            limit++;

        return limit;
    }

    private IEnumerable<EntityUid> IterateAndCheckThralls(Entity<DantalionComponent> dantalion)
    {
        foreach (var thrall in dantalion.Comp.Thralls.ToArray())
        {
            if (!Exists(thrall)
                || !TryComp<VampireThrallComponent>(thrall, out var thrallComp)
                || thrallComp.Master != dantalion.Owner)
            {
                dantalion.Comp.Thralls.Remove(thrall);
                continue;
            }

            yield return thrall;
        }
    }

    private void HealDantalionThralls(Entity<DantalionComponent> ent)
    {
        var uid = ent.Owner;
        var dantalion = ent.Comp;

        if (dantalion.Thralls.Count == 0)
            return;

        foreach (var thrall in IterateAndCheckThralls(ent))
        {
            var healSpec = new DamageSpecifier();
            foreach (var (groupId, amount) in dantalion.ThrallHealGroups)
            {
                if (amount <= 0 || !_proto.TryIndex<DamageGroupPrototype>(groupId, out var group))
                    continue;

                healSpec += new DamageSpecifier(group, -amount);
            }

            foreach (var (typeId, amount) in dantalion.ThrallHealTypes)
            {
                if (amount <= 0 || !_proto.TryIndex<DamageTypePrototype>(typeId, out var type))
                    continue;

                healSpec += new DamageSpecifier(type, -amount);
            }

            if (healSpec.Empty)
                continue;

            _damageableSystem.TryChangeDamage(thrall, healSpec, true);
        }
    }

    #endregion

    #region Pacify

    private void OnPacify(EntityUid uid, DantalionComponent dantalion, ref VampirePacifyActionEvent args)
    {
        if (args.Handled || !Exists(args.Target))
            return;

        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        var target = args.Target;

        if (HasComp<BibleUserComponent>(target) && vampire.FullPower != true)
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!IsValidEnthrallTarget(uid, target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-pacify-invalid"), uid, uid, PopupType.MediumCaution);
            return;
        }

        var duration = args.PacifyDuration;
        if (duration <= TimeSpan.Zero)
            return;

        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity) || !_vampire.CheckAndConsumeBloodCost(uid, vampire, actionEntity))
            return;

        EnsureComp<PacifiedComponent>(target);
        var active = EnsureComp<ActiveVampirePacifyComponent>(target);
        active.EndTime = _timing.CurTime + duration;

        _popup.PopupEntity(Loc.GetString("vampire-pacify-success", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        _popup.PopupEntity(Loc.GetString("vampire-pacify-target", ("duration", Math.Round(args.PacifyDuration.TotalSeconds))), target, target, PopupType.Medium);
        args.Handled = true;
    }

    #endregion

    #region Subspace Swap

    private void OnSubspaceSwap(EntityUid uid, DantalionComponent dantalion, ref VampireSubspaceSwapActionEvent args)
    {
        if (args.Handled || !Exists(args.Target))
            return;

        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        var target = args.Target;

        if (HasComp<BibleUserComponent>(target) && vampire.FullPower != true)
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (TryComp<VampireThrallComponent>(target, out var thrall) && thrall.Master == uid)
        {
            _popup.PopupEntity(Loc.GetString("vampire-subspace-swap-thrall"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<MobStateComponent>(target, out var targetMobState) || targetMobState.CurrentState == MobState.Dead)
        {
            _popup.PopupEntity(Loc.GetString("vampire-subspace-swap-dead"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<MobStateComponent>(uid, out var performerMobState) || performerMobState.CurrentState == MobState.Dead)
            return;

        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity) || !_vampire.CheckAndConsumeBloodCost(uid, vampire, actionEntity))
            return;

        if (!_transform.SwapPositions(uid, target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-subspace-swap-failed"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var slowDuration = args.SlowDuration < TimeSpan.Zero ? TimeSpan.Zero : args.SlowDuration;
        if (slowDuration > TimeSpan.Zero)
        {
            var multiplier = Math.Clamp(args.SlowMultiplier, 0.05f, 1f);
            _movementMod.TryAddMovementSpeedModDuration(target, MovementModStatusSystem.FlashSlowdown, slowDuration, multiplier);
        }

        ApplyHysteriaVision(target, uid, args.HysteriaDuration, args.HysteriaDisguiseSprites);

        _popup.PopupEntity(Loc.GetString("vampire-subspace-swap-success", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        _popup.PopupEntity(Loc.GetString("vampire-subspace-swap-target"), target, target, PopupType.Medium);
        args.Handled = true;
    }

    #endregion

    #region Decoy

    private void OnDecoyActivated(ref VampireDecoyActivatedEvent ev)
    {
        var ent = ev.Dantalion;
        var args = ev.Action;
        var uid = ent.Owner;

        if (ev.InvisibilityDuration > TimeSpan.Zero)
        {
            var active = EnsureComp<ActiveVampireInvisibilityComponent>(uid);
            active.EndTime = _timing.CurTime + ev.InvisibilityDuration;
            active.HadStealthComponent = ev.HadStealthComponent;
            active.PreviousStealthEnabled = ev.PreviousStealthEnabled;
            active.PreviousStealthVisibility = ev.PreviousStealthVisibility;
        }

        var xform = Transform(uid);
        var spawnCoords = _transform.GetMapCoordinates(xform);

        var decoy = EntityManager.SpawnEntity("VampireDecoyEntity", spawnCoords);

        if (TryComp<VampireDecoyAppearanceComponent>(decoy, out var decoyAppearance))
        {
            decoyAppearance.Source = uid;
            Dirty(decoy, decoyAppearance);
        }

        if (TryComp(uid, out MetaDataComponent? performerMeta))
            _metaData.SetEntityName(decoy, performerMeta.EntityName);

        var decoyComp = EnsureComp<VampireDecoyComponent>(decoy);
        decoyComp.Detonated = false;
        decoyComp.DisplayPopup = args.DecoyFlashDisplayPopup;
        decoyComp.Probability = args.DecoyFlashProbability;

        // Set lifetime
        var life = args.DecoyDuration < TimeSpan.Zero ? TimeSpan.Zero : args.DecoyDuration;
        if (life > TimeSpan.Zero)
        {
            var timed = EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(decoy);
            timed.Lifetime = (float) life.TotalSeconds;
        }
    }

    #endregion

    #region Rally Thralls

    private void OnRallyThralls(EntityUid uid, DantalionComponent dantalion, ref VampireRallyThrallsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        var coords = Transform(uid).Coordinates;

        var toRally = new List<EntityUid>();

        foreach (var thrall in IterateAndCheckThralls((uid, dantalion)))
        {
            var thrallCoords = Transform(thrall).Coordinates;
            if (!thrallCoords.TryDistance(EntityManager, _transform, coords, out var distance) || distance > args.Range)
                continue;

            toRally.Add(thrall);
        }

        if (toRally.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("vampire-rally-thralls-none"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity) || !_vampire.CheckAndConsumeBloodCost(uid, vampire, actionEntity))
            return;

        var ralliedCount = 0;

        foreach (var thrall in toRally)
        {
            if (!Exists(thrall))
                continue;

            // Remove stuns
            if (HasComp<StunnedComponent>(thrall))
                RemComp<StunnedComponent>(thrall);

            _statusEffects.TryRemoveStatusEffect(thrall, SharedStunSystem.StunId);
            _stun.TryUnstun(thrall);
            RemComp<KnockedDownComponent>(thrall);

            //Remove sleep
            if (HasComp<SleepingComponent>(thrall))
                RemComp<SleepingComponent>(thrall);

            // Restore stamina
            if (TryComp<StaminaComponent>(thrall, out var stamina))
            {
                stamina.StaminaDamage = 0f;
                _stamina.ExitStamCrit(thrall, stamina);
                _stamina.AdjustStatus((thrall, stamina));
                RemComp<ActiveStaminaComponent>(thrall);
                _statusEffects.TryRemoveStatusEffect(thrall, SharedStaminaSystem.StaminaLow);
                _stamina.UpdateStaminaVisuals((thrall, stamina));
                Dirty(thrall, stamina);
            }

            var rallyEffect = EntityManager.SpawnEntity(dantalion.RallyOverlayEffect, Transform(thrall).Coordinates);
            _transform.SetParent(rallyEffect, thrall);

            ralliedCount++;
        }

        _popup.PopupEntity(Loc.GetString("vampire-rally-thralls-success", ("count", ralliedCount)), uid, uid);

        args.Handled = true;
    }

    #endregion

    #region Blood Bond

    private void OnBloodBondStartAttempt(ref VampireBloodBondStartAttemptEvent ev)
    {
        var ent = ev.Dantalion;
        if (ent.Comp.Thralls.Count != 0)
            return;

        _popup.PopupEntity(Loc.GetString("vampire-blood-bond-no-thralls"), ent, ent, PopupType.MediumCaution);
        ev.Cancelled = true;
    }

    private void OnBloodBondStarted(ref VampireBloodBondStartedEvent ev)
    {
        var ent = ev.Dantalion;
        var args = ev.Action;
        ActivateBloodBond(ent.Owner, ent.Comp, args.Action.Owner, args.Range, args.BloodCostPerTick, args.TickInterval, args.BeamPrototype);
    }

    private void OnBloodBondStopped(ref VampireBloodBondStoppedEvent ev)
        => DeactivateBloodBond(ev.Dantalion.Owner, ev.Dantalion.Comp);

    private void ActivateBloodBond(
        EntityUid uid,
        DantalionComponent dantalion,
        EntityUid actionEntity,
        float range,
        int bloodCostPerTick,
        TimeSpan tickInterval,
        string beamPrototype)
    {
        dantalion.BloodBondActive = true;
        dantalion.BloodBondBeamPrototype = beamPrototype;
        dantalion.BloodBondLoopId++;
        dantalion.BloodBondLinkedThralls.Clear();

        var beamComp = EnsureComp<VampireBloodBondBeamComponent>(uid);
        beamComp.VisualPrototype = beamPrototype;
        beamComp.ActiveBeams.Clear();

        Dirty(uid, dantalion);

        var active = EnsureComp<ActiveVampireBloodBondComponent>(uid);
        active.ActionEntity = actionEntity;
        active.Range = range;
        active.BloodCostPerTick = bloodCostPerTick;
        active.TickInterval = tickInterval;
        active.NextTick = _timing.CurTime;
    }

    private void DeactivateBloodBond(EntityUid uid, DantalionComponent dantalion)
    {
        dantalion.BloodBondActive = false;
        dantalion.BloodBondLinkedThralls.Clear();

        if (TryComp<VampireBloodBondBeamComponent>(uid, out var beamComp))
        {
            foreach (var connection in beamComp.ActiveBeams.Values)
            {
                var removeEvent = new VampireBloodBondBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, beamComp.VisualPrototype);
                RaiseNetworkEvent(removeEvent);
            }

            beamComp.ActiveBeams.Clear();
        }

        RemComp<ActiveVampireBloodBondComponent>(uid);
        Dirty(uid, dantalion);
    }

    private void ProcessActiveBloodBonds(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ActiveVampireBloodBondComponent, DantalionComponent>();
        while (query.MoveNext(out var uid, out var active, out var dantalion))
        {
            if (active.TickInterval <= TimeSpan.Zero)
            {
                DeactivateBloodBond(uid, dantalion);
                continue;
            }

            while (now >= active.NextTick)
            {
                if (!ProcessBloodBondTick(uid, active, dantalion))
                    break;

                active.NextTick += active.TickInterval;
            }
        }
    }

    private bool ProcessBloodBondTick(EntityUid uid, ActiveVampireBloodBondComponent active, DantalionComponent dantalion)
    {
        if (!dantalion.BloodBondActive)
        {
            RemComp<ActiveVampireBloodBondComponent>(uid);
            return false;
        }

        if (TryComp<MobStateComponent>(uid, out var mobState)
            && mobState.CurrentState is MobState.Dead or MobState.Critical)
        {
            DeactivateBloodBond(uid, dantalion);
            if (Exists(active.ActionEntity) && _actions.GetAction(active.ActionEntity) is { } action)
                _actions.SetToggled(action.AsNullable(), false);
            return false;
        }

        if (TryComp<VampireComponent>(uid, out var comp)
            && comp.DrunkBlood < active.BloodCostPerTick)
        {
            DeactivateBloodBond(uid, dantalion);
            _popup.PopupEntity(Loc.GetString("vampire-blood-bond-stop-blood"), uid, uid);
            if (Exists(active.ActionEntity) && _actions.GetAction(active.ActionEntity) is { } action)
                _actions.SetToggled(action.AsNullable(), false);
            return false;
        }

        if (comp != null)
            _vampire.CheckAndConsumeBloodCost(uid, comp, null, active.BloodCostPerTick);

        var coords = Transform(uid).Coordinates;
        var linkedThralls = new List<EntityUid>();

        foreach (var thrall in IterateAndCheckThralls((uid, dantalion)))
        {
            var thrallCoords = Transform(thrall).Coordinates;
            if (!thrallCoords.TryDistance(EntityManager, _transform, coords, out var distance) || distance > active.Range)
                continue;

            // Prevent bond beams working through walls
            if (!_examine.InRangeUnOccluded(uid, thrall, active.Range))
                continue;

            if (TryComp<MobStateComponent>(thrall, out var thrallMobState)
                && thrallMobState.CurrentState != MobState.Dead)
            {
                linkedThralls.Add(thrall);
            }
        }

        dantalion.BloodBondLinkedThralls = linkedThralls;
        Dirty(uid, dantalion);
        UpdateBloodBondBeamNetwork(uid, linkedThralls, active.Range);
        return true;
    }

    private void OnDantalionDamage(EntityUid uid, DantalionComponent dantalion, ref DamageBeforeApplyEvent args)
    {
        if (!dantalion.BloodBondActive || dantalion.BloodBondProcessingDamage)
            return;

        SplitBloodBondDamage(uid, uid, dantalion, ref args);
    }

    private void OnThrallDamage(EntityUid uid, VampireThrallComponent thrall, ref DamageBeforeApplyEvent args)
    {
        if (!TryComp<DantalionComponent>(thrall.Master, out var dantalion))
            return;

        if (!dantalion.BloodBondActive || dantalion.BloodBondProcessingDamage)
            return;

        if (!dantalion.BloodBondLinkedThralls.Contains(uid))
            return;

        SplitBloodBondDamage(uid, thrall.Master.Value, dantalion, ref args);
    }

    private void SplitBloodBondDamage(EntityUid damagedEntity, EntityUid vampire, DantalionComponent dantalion, ref DamageBeforeApplyEvent args)
    {
        if (args.Damage.GetTotal() <= 0)
            return;

        var participants = new List<EntityUid> { vampire };
        foreach (var thrall in dantalion.BloodBondLinkedThralls)
        {
            if (Exists(thrall))
                participants.Add(thrall);
        }

        if (participants.Count < 2)
            return;

        var totalDamage = FixedPoint2.Zero;
        foreach (var (_, value) in args.Damage.DamageDict)
        {
            if (value > 0)
                totalDamage += value;
        }

        var originalTargetDamage = new DamageSpecifier();
        var redistributedDamage = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > 0)
            {
                var shared = value / participants.Count;
                originalTargetDamage.DamageDict[type] = shared;
                redistributedDamage.DamageDict[type] = shared;
            }
            else
            {
                originalTargetDamage.DamageDict[type] = value;
            }
        }
        args.Damage = originalTargetDamage;

        dantalion.BloodBondProcessingDamage = true;

        foreach (var other in participants)
        {
            if (other == damagedEntity)
                continue;

            if (!Exists(other))
                continue;

            _damageableSystem.TryChangeDamage(other, redistributedDamage, ignoreResistances: true, origin: args.Origin);
        }

        dantalion.BloodBondProcessingDamage = false;
    }

    private void UpdateBloodBondBeamNetwork(EntityUid vampire, List<EntityUid> targets, float range)
    {
        if (!TryComp<VampireBloodBondBeamComponent>(vampire, out var beamComp))
            return;

        var requiredTargets = new HashSet<EntityUid>(targets);

        var toRemove = new List<EntityUid>();
        foreach (var (targetKey, connection) in beamComp.ActiveBeams)
        {
            if (connection.Source != vampire)
            {
                var removeLegacy = new VampireBloodBondBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, beamComp.VisualPrototype);
                RaiseNetworkEvent(removeLegacy);
                toRemove.Add(targetKey);
                continue;
            }

            if (!requiredTargets.Contains(connection.Target))
            {
                var removeEvent = new VampireBloodBondBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, beamComp.VisualPrototype);
                RaiseNetworkEvent(removeEvent);
                toRemove.Add(targetKey);
            }
        }

        foreach (var key in toRemove)
            beamComp.ActiveBeams.Remove(key);

        foreach (var target in requiredTargets)
        {
            if (!beamComp.ActiveBeams.ContainsKey(target))
            {
                var connection = new BloodBondBeamConnection(vampire, target, range);
                beamComp.ActiveBeams[target] = connection;

                var createEvent = new VampireBloodBondBeamEvent(GetNetEntity(vampire), GetNetEntity(target), true, beamComp.VisualPrototype);
                RaiseNetworkEvent(createEvent);
            }
        }
    }

    #endregion

    #region Mass Hysteria

    private void OnMassHysteria(EntityUid uid, DantalionComponent dantalion, ref VampireMassHysteriaActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<VampireComponent>(uid, out var vampire))
            return;

        if (!vampire.FullPower)
        {
            _popup.PopupEntity(Loc.GetString("action-vampire-not-enough-power"), uid, uid);
            args.Handled = true;
            return;
        }

        var actionEntity = args.Action.Owner;
        if (!Exists(actionEntity) || !_vampire.CheckAndConsumeBloodCost(uid, vampire, actionEntity))
            return;

        var coords = Transform(uid).Coordinates;

        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var target, out _, out var mobState, out var xform))
        {
            if (target == uid)
                continue;

            if (mobState.CurrentState == MobState.Dead)
                continue;

            if (!xform.Coordinates.TryDistance(EntityManager, _transform, coords, out var distance) || distance > args.Range)
                continue;

            if (HasComp<VampireThrallComponent>(target))
                continue;

            _flash.Flash(target, uid, null, args.FlashDuration, 0.8f, false);

            if (TryComp<ActorComponent>(target, out var actor))
                _audio.PlayGlobal(args.Sound, actor.PlayerSession, AudioParams.Default.WithVolume(1f));

            ApplyHysteriaVision(target, uid, args.HysteriaDuration, args.HysteriaDisguiseSprites);
        }

        args.Handled = true;
    }

    private void ApplyHysteriaVision(EntityUid target, EntityUid source, TimeSpan duration, List<HysteriaDisguiseSprite> disguiseSprites)
    {
        var hysteria = EnsureComp<HysteriaVisionComponent>(target);
        hysteria.EndTime = _timing.CurTime + duration;
        hysteria.Source = source;
        hysteria.DisguiseSprites = disguiseSprites;
        Dirty(target, hysteria);
    }

    #endregion
}
