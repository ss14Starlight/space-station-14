using Content.Server.Bible.Components;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Prototypes;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Physics;
using Content.Shared.Speech.Muting;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Popups;
using Content.Server.EUI;
using Robust.Shared.Player;
using Content.Shared.Flash;
using Content.Shared.Bed.Sleep;
using Content.Shared.StatusEffect;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared._FarHorizons.Silicons.IPC.Components;

namespace Content.Server._Starlight.Antags.Vampires.Systems;

public sealed partial class VampireSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly EuiManager _euiMan = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly Content.Shared.Mind.SharedMindSystem _mind = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    private static readonly SoundSpecifier _biteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");
    private static readonly SoundSpecifier _devourSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");
    private readonly Dictionary<EntityUid, List<EntityUid>> _playerShadowSnares = new();

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<VampireComponent, VampireToggleFangsActionEvent>(OnToggleFangs);

        SubscribeLocalEvent<VampireComponent, VampireGlareActionEvent>(OnGlare);

        SubscribeLocalEvent<VampireComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VampireComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<VampireComponent, VampireDrinkBloodDoAfterEvent>(OnDrinkDoAfter);

        SubscribeLocalEvent<VampireDevourableComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VampireComponent, VampireDevourDoAfterEvent>(OnDevourDoAfter);

        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIActionEvent>(OnRejuvenateI);
        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIIActionEvent>(OnRejuvenateII);

        SubscribeLocalEvent<VampireComponent, VampireClassSelectActionEvent>(OnClassSelect);

        Subs.BuiEvents<VampireComponent>(VampireClassUiKey.Key, subs =>
        {
            subs.Event<VampireClassChosenBuiMsg>(OnVampireClassChosen);
            subs.Event<VampireClassClosedBuiMsg>(OnVampireClassClosed);
        });
    }

    private void OnUseInHand(Entity<VampireDevourableComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (!TryComp<VampireComponent>(user, out var vamp))
            return;

        if (IsMouthBlocked(user))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), user, user);
            return;
        }

        if (vamp.MaxBloodFullness > 0f && vamp.BloodFullness >= vamp.MaxBloodFullness)
            return;

        if (!Exists(ent.Owner))
            return;

        var doAfterEv = new VampireDevourDoAfterEvent
        {
            BloodFullnessRestore = ent.Comp.BloodFullnessRestore
        };

        var dargs = new DoAfterArgs(EntityManager, user, ent.Comp.DevourDelay, doAfterEv, user, used: ent.Owner)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = false,
            BreakOnDamage = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        if (_doAfter.TryStartDoAfter(dargs))
            args.Handled = true;
    }

    private void OnDevourDoAfter(EntityUid uid, VampireComponent comp, ref VampireDevourDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
            return;

        if (args.Used is not { } used || !Exists(used))
            return;

        var wasStarving = comp.BloodFullness <= 0f;
        comp.BloodFullness = MathF.Min(comp.MaxBloodFullness, comp.BloodFullness + args.BloodFullnessRestore);
        var isStarving = comp.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(uid);

        Dirty(uid, comp);
        UpdateVampireFedAlert(uid, comp);

        _audio.PlayPvs(_devourSound, uid);
        QueueDel(used);

        args.Handled = true;
    }

    #region Helper Methods

    /// <summary>
    /// Check if tile coordinates are valid and not blocked
    /// </summary>
    internal bool IsValidTile(EntityCoordinates coords, EntityUid? gridUid = null, MapGridComponent? gridComp = null)
    {
        gridUid ??= _transform.GetGrid(coords);
        if (gridUid == null
            || (gridComp == null && !TryComp(gridUid.Value, out gridComp))
            || !_map.TryGetTileRef(gridUid.Value, gridComp, coords, out var tileRef))
            return false;

        return !_turf.IsSpace(tileRef) &&
            !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable) &&
            !IsTileBlockedByEntities(coords);
    }

    internal bool HasChosenClass(EntityUid uid)
        => TryComp<VampireComponent>(uid, out var vamp) && !string.IsNullOrWhiteSpace(vamp.ChosenClassId);

    internal bool ValidateVampireClass(EntityUid uid, VampireComponent comp, ProtoId<VampireClassPrototype>? requiredClass)
    {
        _ = uid;
        if (requiredClass == null)
            return true;

        return string.Equals(comp.ChosenClassId, requiredClass.Value.Id, StringComparison.Ordinal);
    }

    /// <summary>
    /// Common validation for vampire abilities
    /// component check + class validation + action cost
    /// </summary>
    internal bool ValidateVampireAbility(EntityUid uid, [NotNullWhen(true)] out VampireComponent? comp, ProtoId<VampireClassPrototype>? requiredClass = null, EntityUid? actionEntity = null)
    {
        if (!TryComp(uid, out comp))
            return false;

        if (!ValidateVampireClass(uid, comp, requiredClass))
            return false;

        if (actionEntity.HasValue && !CheckAndConsumeBloodCost(uid, comp, actionEntity.Value))
            return false;

        return true;
    }

    /// <summary>
    /// Unified blood cost checking and consumption
    /// </summary>
    internal bool CheckAndConsumeBloodCost(EntityUid uid, VampireComponent comp, EntityUid? actionEntity = null, int bloodCost = 0)
    {

        if (bloodCost <= 0 && actionEntity != null && TryComp<VampireActionComponent>(actionEntity.Value, out var vac))
        {
            if (comp.TotalBlood < vac.BloodToUnlock)
                return false;

            if (vac.BloodCost > 0)
                bloodCost = (int)vac.BloodCost;
        }
        else if (bloodCost <= 0)
        {
            _sawmill?.Error($"No action entity or no VampireActionComponent found for: {uid.ToString()}!");
            return false;
        }

        if (bloodCost <= 0)
            return true;

        if (comp.DrunkBlood < bloodCost)
        {
            _popup.PopupEntity(Loc.GetString("vampire-not-enough-blood"), uid, uid);
            return false;
        }

        comp.DrunkBlood -= bloodCost;
        Dirty(uid, comp);
        UpdateVampireAlert(uid);
        return true;
    }
    internal bool CheckAndConsumeActionCost(EntityUid uid, VampireComponent comp, EntityUid? actionEntity)
        => CheckAndConsumeBloodCost(uid, comp, actionEntity);

    internal bool IsProtectedByFaith(EntityUid target)
        => HasComp<BibleUserComponent>(target);

    private bool IsInvalidDrinkTarget(EntityUid user, EntityUid target, bool showPopup = true)
    {
        if (!HasComp<VampireComponent>(target) && !HasComp<VampireThrallComponent>(target) && !HasComp<IPCBatteryComponent>(target))
            return false;

        if (showPopup)
            _popup.PopupEntity(Loc.GetString("vampire-drink-invalid-target"), user, user, PopupType.MediumCaution);

        return true;
    }

    /// <summary>
    /// Checks if a tile position is blocked by solid entities(walls etc.)
    /// </summary>
    internal bool IsTileBlockedByEntities(EntityCoordinates coords)
    {
        // Check for anchored entities in this position that block movement
        foreach (var ent in _lookup.GetEntitiesIntersecting(_transform.ToMapCoordinates(coords), LookupFlags.Static))
        {
            // Skip non anchored entities
            if (!Transform(ent).Anchored)
                continue;

            // Check if entity has a physics component with impassable collision
            if (TryComp<PhysicsComponent>(ent, out var physics) &&
                physics.CanCollide &&
                (physics.CollisionMask & (int)CollisionGroup.Impassable) != 0)
                return true;

            // Check for door components that typically block movement
            if (HasComp<Shared.Doors.Components.DoorComponent>(ent))
                return true;

            // Check entity prototype names for common wall/structure types
            if (TryComp(ent, out MetaDataComponent? meta) &&
                meta.EntityPrototype?.ID != null)
            {
                var id = meta.EntityPrototype.ID.ToLower();
                if (id.Contains("wall") || id.Contains("grille") || id.Contains("window") ||
                    id.Contains("reinforced") || id.Contains("solid"))
                    return true;
            }
        }
        return false;
    }

    #endregion

    #region Base Abilities
    private void OnToggleFangs(EntityUid uid, VampireComponent comp, ref VampireToggleFangsActionEvent args)
    {
        if (args.Handled)
            return;

        comp.FangsExtended = !comp.FangsExtended;
        if (!comp.FangsExtended)
            comp.IsDrinking = false;

        if (comp.ActionEntities.TryGetValue("ActionVampireToggleFangs", out var actionEntity) && _actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), comp.FangsExtended);
        Dirty(uid, comp);
        args.Handled = true;
    }

    private void OnAfterInteract(EntityUid uid, VampireComponent comp, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !comp.FangsExtended || !Exists(args.Target))
            return;

        var target = args.Target.Value;

        if (target == uid
            || !HasComp<BloodstreamComponent>(target)
            )
            return;

        if (IsInvalidDrinkTarget(uid, target))
            return;

        if (IsProtectedByFaith(target) && comp.FullPower != true)
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (IsMouthBlocked(uid))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), uid, uid);
            return;
        }

        StartDrinkDoAfter(uid, comp, target, showPopup: true);
        args.Handled = true;
    }

    private void OnBeforeInteractHand(EntityUid uid, VampireComponent comp, ref BeforeInteractHandEvent args)
    {
        if (args.Handled || !comp.FangsExtended)
            return;

        var target = args.Target;
        if (!Exists(target)
            || target == uid
            || !HasComp<BloodstreamComponent>(target)
            )
            return;

        if (IsInvalidDrinkTarget(uid, target))
            return;

        if (IsProtectedByFaith(target) && comp.FullPower != true)
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (IsMouthBlocked(uid))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), uid, uid);
            return;
        }

        StartDrinkDoAfter(uid, comp, target, showPopup: true);
        args.Handled = true;
    }

    private void OnDrinkDoAfter(EntityUid uid, VampireComponent comp, ref VampireDrinkBloodDoAfterEvent args)
    {
        if (args.Handled)
            return;

        var wasStarving = comp.BloodFullness <= 0f;

        if (args.Cancelled)
        {
            comp.IsDrinking = false;
            return;
        }

        if (!comp.FangsExtended
            || args.Args.Target == null
            || !HasComp<BloodstreamComponent>(args.Args.Target.Value)
            )
        {
            comp.IsDrinking = false;
            return;
        }

        var target = args.Args.Target.Value;

        if (IsInvalidDrinkTarget(uid, target, showPopup: false))
        {
            comp.IsDrinking = false;
            return;
        }

        if (!comp.BloodDrunkFromTargets.TryGetValue(target, out var drunkFromTarget))
            drunkFromTarget = 0;

        if (drunkFromTarget >= comp.MaxBloodPerTarget)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-maxed", ("amount", comp.MaxBloodPerTarget)), uid, uid, Shared.Popups.PopupType.MediumCaution);
            comp.IsDrinking = false;
            return;
        }

        var sipInefficiency = 0f;
        var sipAmount = comp.sipAmount;

        if (HasComp<HumanoidAppearanceComponent>(args.Args.Target.Value))
        {
            sipInefficiency = 1f / 0.5f;  //comp.humanoidEfficiency;
            sipAmount = comp.sipAmount; // comp.sipAmountHuman; //todo make yml work
        }
        else
        {
            sipInefficiency = 1f / 0.125f; //comp.nonHumanoidEfficiency;
            sipAmount = comp.sipAmount / 4; //comp.sipAmountNonHuman; //todo make yml work
        }

        var maxCanDrink = comp.MaxBloodPerTarget - drunkFromTarget;
        var actualSipAmount = MathF.Min(sipAmount, maxCanDrink);

        //attempt to drain the target's blood level
        if (_blood.TryModifyBloodLevel(target, -actualSipAmount * sipInefficiency))
        {
            //Blood level reduction success
            comp.DrunkBlood += (int)actualSipAmount;

            //Confirm target is a humanoid before progressing objectives
            if (HasComp<HumanoidAppearanceComponent>(args.Args.Target.Value))
            {
                comp.TotalBlood += (int)actualSipAmount;
            }

            //Biting Damage
            //Little bit of additional damage to disincentivize blood donations
            var BiteDamage = new DamageSpecifier();
            //BiteDamage += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_poisonTypeId), FixedPoint2.New(0.1) * actualSipAmount); //1 cellular per 10u
            BiteDamage += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_pierceTypeId), FixedPoint2.New(0.05) * actualSipAmount); //5 pierce per 10u
            _damageableSystem.TryChangeDamage(target, BiteDamage, ignoreResistances: true);
            _blood.TryModifyBleedAmount(target, 1);


            //Add in blindness instead of cancer
            if (TryComp<BlindableComponent>(target, out var blindable) && 2 <= comp.BlindInc)
            {
                _blindable.AdjustEyeDamage((target, blindable), 1);
                comp.BlindInc = 0;
                //var timeSpan = TimeSpan.FromSeconds(3f);
                //_statusEffect.TryAddStatusEffect(target, TemporaryBlindnessSystem.BlindingStatusEffect, timeSpan, false, TemporaryBlindnessSystem.BlindingStatusEffect);
            }
            else if (comp.BlindInc < 2)
            {
                comp.BlindInc += 1;
            }
            RaiseLocalEvent(uid, new VampireProgressionChangedEvent());

            if (!comp.BloodDrunkFromTargets.ContainsKey(target))
                comp.BloodDrunkFromTargets[target] = 0;
            comp.BloodDrunkFromTargets[target] += (int)actualSipAmount;

            comp.BloodFullness = MathF.Min(comp.MaxBloodFullness, comp.BloodFullness + actualSipAmount);

            var isStarving = comp.BloodFullness <= 0f;
            if (wasStarving != isStarving)
                _movementSpeed.RefreshMovementSpeedModifiers(uid);

            // Base healing
            var baseHealSpec = new DamageSpecifier();
            baseHealSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_bruteGroupId), -FixedPoint2.New(2));
            baseHealSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_burnGroupId), -FixedPoint2.New(2));
            baseHealSpec += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_poisonTypeId), -FixedPoint2.New(4));
            baseHealSpec += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_oxyLossTypeId), -FixedPoint2.New(10));
            _damageableSystem.TryChangeDamage(uid, baseHealSpec, true);

            RaiseLocalEvent(uid, new VampireBloodDrankEvent(target, actualSipAmount));

            UpdateFullPower(uid, comp);

            _audio.PlayPvs(_biteSound, target, AudioParams.Default.WithVolume(-7f));
            var targetCoords = Transform(target).Coordinates;
            Spawn("WeaponArcBite", targetCoords);

            Dirty(uid, comp);

            UpdateVampireAlert(uid);
            UpdateVampireFedAlert(uid, comp);

            var currentDrunkFromTarget = comp.BloodDrunkFromTargets.GetValueOrDefault(target, 0);
            if (comp.FangsExtended && currentDrunkFromTarget < comp.MaxBloodPerTarget)
            {
                comp.IsDrinking = false;
                StartDrinkDoAfter(uid, comp, target, showPopup: false);
            }
            else
            {
                comp.IsDrinking = false;
                if (currentDrunkFromTarget >= comp.MaxBloodPerTarget)
                    _popup.PopupEntity(Loc.GetString("vampire-drink-target-hard-max", ("amount", comp.MaxBloodPerTarget)), uid, uid);
            }
        }
        else
            comp.IsDrinking = false; //Blood level reduction failed
    }

    partial void UpdateVampireAlert(EntityUid uid)
        => _alerts.ShowAlert(uid, "VampireBlood");

    partial void UpdateVampireFedAlert(EntityUid uid, VampireComponent? comp)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var frac = comp.MaxBloodFullness <= 0f ? 0f : comp.BloodFullness / comp.MaxBloodFullness;
        var sev = (short)Math.Clamp((int)MathF.Ceiling(frac * 4f) + 1, 1, 5);
        _alerts.ShowAlert(uid, "VampireFed", sev);
    }

    private void StartDrinkDoAfter(EntityUid uid, VampireComponent comp, EntityUid target, bool showPopup)
    {
        if (comp.IsDrinking)
            return;

        if (IsMouthBlocked(uid))
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), uid, uid);
            return;
        }

        var dargs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(1.25), new VampireDrinkBloodDoAfterEvent(), uid, target)
        {
            DistanceThreshold = 1.5f,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        if (_doAfter.TryStartDoAfter(dargs))
        {
            comp.IsDrinking = true;
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("vampire-drink-start", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        }
    }

    private void OnGlare(EntityUid uid, VampireComponent comp, ref VampireGlareActionEvent args)
    {
        if (args.Handled
            || !comp.ActionEntities.TryGetValue("ActionVampireGlare", out var actionEntity)
            || !CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        // Find targets within 1 tile around the vampire
        var targets = _lookup.GetEntitiesInRange(uid, args.Range, LookupFlags.Dynamic | LookupFlags.Sundries);

        var ourXform = Transform(uid);
        var ourDirection = ourXform.LocalRotation.ToVec();
        var ourPosition = ourXform.LocalPosition;

        foreach (var target in targets)
        {
            if (target == uid)
                continue;

            var targetPosition = Transform(target).LocalPosition;
            var vectorToTarget = Vector2.Normalize(targetPosition - ourPosition);

            var dot = Vector2.Dot(ourDirection, vectorToTarget);

            var duration = TimeSpan.FromSeconds(10);
            _statusEffects.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, duration);

            if (!TryComp<StaminaComponent>(target, out var stam))
                continue;

            var knockedDown = HasComp<KnockedDownComponent>(target);

            // If target in front
            if (dot > 0.7f && !knockedDown)
            {
                _stun.TryAddParalyzeDuration(target, TimeSpan.FromSeconds(2));

                _stamina.TakeStaminaDamage(target, args.FrontStaminaDamage, stam, source: uid);

                // Mute for 8 second
                EnsureComp<MutedComponent>(target);
                Timer.Spawn(args.MuteDuration, () =>
                {
                    if (Exists(target))
                        RemComp<MutedComponent>(target);
                });

                StartGlareDotEffect(target, uid, args.DotStaminaDamage, 0, true);

                return;
            }
            // If target behind
            else if (dot < -0.7f && !knockedDown)
                _stamina.TakeStaminaDamage(target, args.BehindStaminaDamage, stam, source: uid);
            else
            {
                _stun.TryAddParalyzeDuration(target, TimeSpan.FromSeconds(4));

                _stamina.TakeStaminaDamage(target, args.SideStaminaDamage, stam, source: uid);
            }

            // Start DOT effect with limited ticks
            StartGlareDotEffect(target, uid, args.DotStaminaDamage, 0, false);
        }

        args.Handled = true;
    }

    private void StartGlareDotEffect(EntityUid target, EntityUid source, float damage, int tickCount, bool doStaminaDamage)
    {
        const int MaxTicks = 10;

        if (tickCount >= MaxTicks || !Exists(target) || !Exists(source))
            return;

        if (doStaminaDamage && TryComp<StaminaComponent>(target, out var stam) && !stam.Critical)
            _stamina.TakeStaminaDamage(target, damage, stam, source: source);

        Timer.Spawn(TimeSpan.FromSeconds(1), () => StartGlareDotEffect(target, source, damage, tickCount + 1, doStaminaDamage));
    }

    private void OnRejuvenateI(EntityUid uid, VampireComponent comp, ref VampireRejuvenateIActionEvent args)
    {
        if (args.Handled
            || !comp.ActionEntities.TryGetValue("ActionVampireRejuvenateI", out var actionEntity)
            || !CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        if (TryComp<StaminaComponent>(uid, out var stamina))
        {
            stamina.StaminaDamage = 0f;
            _stamina.ExitStamCrit(uid, stamina);
            _stamina.AdjustStatus((uid, stamina));
            RemComp<ActiveStaminaComponent>(uid);
            _statusEffects.TryRemoveStatusEffect(uid, SharedStaminaSystem.StaminaLow);
            _stamina.UpdateStaminaVisuals((uid, stamina));
            Dirty(uid, stamina);
        }

        _statusEffects.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
        _stun.TryUnstun(uid);
        RemComp<KnockedDownComponent>(uid);

        args.Handled = true;
    }

    private void OnRejuvenateII(EntityUid uid, VampireComponent comp, ref VampireRejuvenateIIActionEvent args)
    {
        if (args.Handled
            || !comp.ActionEntities.TryGetValue("ActionVampireRejuvenateII", out var actionEntity)
            || !CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        if (TryComp<StaminaComponent>(uid, out var stamina))
        {
            stamina.StaminaDamage = 0f;
            _stamina.ExitStamCrit(uid, stamina);
            _stamina.AdjustStatus((uid, stamina));
            RemComp<ActiveStaminaComponent>(uid);
            _statusEffects.TryRemoveStatusEffect(uid, SharedStaminaSystem.StaminaLow);
            _stamina.UpdateStaminaVisuals((uid, stamina));
            Dirty(uid, stamina);
        }

        _statusEffects.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
        _stun.TryUnstun(uid);
        RemComp<KnockedDownComponent>(uid);

        // Purge 10u of harmful reagents
        FixedPoint2 MaxRemove = FixedPoint2.New(10);

        if (!TryComp<BloodstreamComponent>(uid, out var blood)
            || !_solution.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var bloodstreamSolution))
            return;

        var solEnt = blood.BloodSolution.Value;

        var toRemove = FixedPoint2.Zero;

        foreach (var quant in bloodstreamSolution.Contents.ToArray())
        {
            if (toRemove >= MaxRemove)
                break;

            if (!_proto.TryIndex<ReagentPrototype>(quant.Reagent.Prototype, out var proto)
                || proto.Metabolisms == null
                || !proto.Metabolisms.Keys.Any(k => k.Id.Equals("Poison", StringComparison.OrdinalIgnoreCase)))
                continue;

            var remaining = MaxRemove - toRemove;
            var removeAmt = FixedPoint2.Min(quant.Quantity, remaining);

            _solution.RemoveReagent(solEnt, quant.Reagent, removeAmt);
            toRemove += removeAmt;
        }

        // Heal over-time in 5 cycles, 3.5s apart: per tick heal Oxy 5, Brute/Burn/Toxin 4
        const int TotalTicks = 5;
        var interval = TimeSpan.FromSeconds(3.5);

        void DoHealTick(int remaining)
        {
            if (!Exists(uid))
                return;

            var healSpec = new DamageSpecifier();
            healSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_bruteGroupId), -FixedPoint2.New(4));
            healSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_burnGroupId), -FixedPoint2.New(4));
            healSpec += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_poisonTypeId), -FixedPoint2.New(4));
            healSpec += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_oxyLossTypeId), -FixedPoint2.New(5));
            _damageableSystem.TryChangeDamage(uid, healSpec, true);

            if (remaining > 1)
                Timer.Spawn(interval, () => DoHealTick(remaining - 1));
        }

        DoHealTick(TotalTicks);

        args.Handled = true;
    }

    private void OnClassSelect(EntityUid uid, VampireComponent comp, ref VampireClassSelectActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasChosenClass(uid))
        {
            args.Handled = true;
            return;
        }

        OpenClassUi(uid, comp);
        args.Handled = true;
        Dirty(uid, comp);
    }

    #endregion

    #region Full Power, Passives
    private void UpdateFullPower(EntityUid uid, VampireComponent comp)
    {
        int uniqueHumanoids = 0;
        foreach (var kv in comp.BloodDrunkFromTargets.Keys)
            if (Exists(kv) && HasComp<HumanoidAppearanceComponent>(kv))
                uniqueHumanoids++;
        comp.UniqueHumanoidVictims = uniqueHumanoids;
        var prev = comp.FullPower;
        comp.FullPower = comp.TotalBlood > 1000 && uniqueHumanoids >= 8;
        if (!prev && comp.FullPower)
        {
            _popup.PopupEntity(Loc.GetString("vampire-full-power-achieved"), uid, uid);
            var ev = new VampireFullPowerAchievedEvent();
            RaiseLocalEvent(uid, ev);
        }
        Dirty(uid, comp);
    }

    private bool IsMouthBlocked(EntityUid uid)
    {
        if (!HasComp<InventoryComponent>(uid))
            return false;

        var slots = new[] { "mask", "head" };
        foreach (var slot in slots)
            if (_inventory.TryGetSlotEntity(uid, slot, out var ent) &&
                TryComp<IngestionBlockerComponent>(ent.Value, out var blocker) &&
                blocker.Enabled)

                return true;

        return false;
    }

    #endregion
}
