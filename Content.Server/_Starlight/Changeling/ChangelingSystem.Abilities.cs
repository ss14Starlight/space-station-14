using Content.Server._Starlight.Language;
using Content.Server._Starlight.Objectives.Components;
using Content.Server.Changeling.Systems;
using Content.Server.Ensnaring;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Changeling;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Light.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mindshield.FakeMindShield;
using Content.Shared.Mobs;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.RetractableItemAction;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stealth.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffect = default!;
    [Dependency] private ChangelingIdentitySystem _changelingIdentitySystem = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private SharedFlashSystem _flashSystem = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private StomachSystem _stomach = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private EnsnareableSystem _ensnareable = default!;

    private static readonly ProtoId<ReagentPrototype> FerrochromicAcidPrototype = "FerrochromicAcid";
    private static readonly ProtoId<ReagentPrototype> PolytrinicAcidPrototype = "PolytrinicAcid";
    private static readonly ProtoId<TagPrototype> BolaTag = "Bola";

    #region Basic Abilities

    [SubscribeLocalEvent]
    private void OnOpenEvolutionMenu(EntityUid uid, ChangelingComponent comp, ref OpenEvolutionMenuEvent args)
    {
        if (!TryComp<StoreComponent>(uid, out var store))
            return;

        _store.ToggleUi(uid, uid, store);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnAbsorb(EntityUid uid, ChangelingComponent comp, ref AbsorbDNAEvent args)
    {
        var target = args.Target;

        if (!IsIncapacitated(target))
        {
            _popup.PopupEntity(Loc.GetString("changeling-absorb-fail-incapacitated"), uid, uid);
            return;
        }
        if (HasComp<AbsorbedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("changeling-absorb-fail-absorbed"), uid, uid);
            return;
        }
        if (!HasComp<AbsorbableComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("changeling-absorb-fail-unabsorbable"), uid, uid);
            return;
        }

        var popupOthers = Loc.GetString("changeling-absorb-start", ("user", Identity.Entity(uid, EntityManager)), ("target", Identity.Entity(target, EntityManager)));
        _popup.PopupEntity(popupOthers, uid, PopupType.LargeCaution);
        PlayMeatySound(uid, comp);
        var dargs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(15), new AbsorbDNADoAfterEvent(), uid, target)
        {
            DistanceThreshold = 1.5f,
            BreakOnDamage = true,
            BreakOnHandChange = false,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };
        args.Handled = _doAfter.TryStartDoAfter(dargs);
    }
    public ProtoId<DamageGroupPrototype> AbsorbedDamageGroup = "Genetic";
    [SubscribeLocalEvent]
    private void OnDevourEvolutionReward(EntityUid uid, ChangelingComponent comp, ref ChangelingDevourFinishedEvent args)
    {
        if (!args.Completed || args.Handled)
            return;
        AwardEvolutionPoints(uid, args.Target);
    }
    private void AwardEvolutionPoints(EntityUid uid, EntityUid target)
    {
        if (HasComp<AbsorbedComponent>(target))
            return;
        var points = HasComp<ChangelingComponent>(target) ? 10 : HasComp<HumanoidAppearanceComponent>(target) ? 2 : 0;
        if (points == 0)
            return;
        var owner = uid;
        if (!TryComp<StoreComponent>(owner, out var store))
        {
            Log.Error($"Cannot award evolution points to {ToPrettyString(uid)}: missing store on {ToPrettyString(owner)}.");
            return;
        }
        if (!_store.TryAddCurrency(new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { { "EvolutionPoint", points } }, owner, store))
        {
            Log.Error($"Cannot award evolution points to {ToPrettyString(uid)}: store {ToPrettyString(owner)} rejected EvolutionPoint.");
            return;
        }
        _store.UpdateUserInterface(uid, uid, store);
    }

    [SubscribeLocalEvent]
    private void OnDevouredPerson(EntityUid uid, ChangelingComponent comp, ref OnLingDevour args)
    {
        var target = args.Consumed;

        if (!IsIncapacitated(target) || HasComp<AbsorbedComponent>(target))
            return;

        PlayMeatySound(uid, comp);

        UpdateBiomass(uid, comp, comp.MaxBiomass - comp.TotalAbsorbedEntities);

        // Starlight edit start - Do not turn the victim's blood into ferrochromic acid permanently
        // allows for ling tests to still pass despite being hollowed.
        if (TryComp<BloodstreamComponent>(target, out var bloodstream) && bloodstream.BloodReferenceSolution is { } originalBlood)
        {
            var blood = originalBlood.Clone();
            blood.ScaleTo(originalBlood.Volume);
            var ferroAcid = new ReagentQuantity(FerrochromicAcidPrototype, originalBlood.Volume);

            _blood.ChangeBloodReagents(target, new Solution([ferroAcid]));
            _blood.SpillAllSolutions(target);
            _blood.ChangeBloodReagents(target, blood);
        }
        // Starlight edit end

        EnsureComp<AbsorbedComponent>(target);
        if (TryComp<PerishableComponent>(target, out var perishable))
            _rotting.SetRotAfter(target, TimeSpan.FromMinutes(20), perishable);

        var popup = Loc.GetString("changeling-absorb-end-self-ling");
        var bonusChemicals = 0f;
        if (TryComp<ChangelingComponent>(target, out var targetComp))
        {
            bonusChemicals += targetComp.MaxChemicals / 2;
            comp.MaxBiomass += targetComp.MaxBiomass / 2;
        }
        else if (HasComp<HumanoidAppearanceComponent>(target))  // Starlight edit
        {
            popup = Loc.GetString("changeling-absorb-end-self");
            bonusChemicals += 10;
        }
        // Starlight edit start
        else
        {
            popup = Loc.GetString("changeling-absorb-end-self");
            bonusChemicals += 5;
        }
        // Starlight edit end
        TryStealDNA(uid, target, comp, true);
        comp.TotalAbsorbedEntities++;

        _popup.PopupEntity(popup, uid, uid);
        comp.MaxChemicals += bonusChemicals;

        if (_mind.TryGetMind(uid, out var mindId, out var mind))
            if (_mind.TryGetObjectiveComp<AbsorbConditionComponent>(mindId, out var objective, mind))
                objective.Absorbed += 1;
    }
    [SubscribeLocalEvent]
    private void OnStingExtractDNA(EntityUid uid, ChangelingComponent comp, ref StingExtractDNAEvent args)
    {
        if (!TrySting(uid, args, true))
            return;

        var target = args.Target;
        if (!TryComp<ChangelingIdentityComponent>(uid, out var identityStorage)
            || _changelingIdentitySystem.CloneToPausedMap((uid, identityStorage), target) == null)
            return;
        _popup.PopupEntity(Loc.GetString("changeling-sting", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnEnterStasis(EntityUid uid, ChangelingComponent comp, ref EnterStasisEvent args)
    {
        if (comp.IsInStasis || HasComp<AbsorbedComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-stasis-enter-fail"), uid, uid);
            return;
        }

        comp.Chemicals = 0f;

        ToggleChameleonSkin(uid, comp, false);

        if (_mobState.IsAlive(uid))
        {
            // fake our death
            var othersMessage = Loc.GetString("suicide-command-default-text-others", ("name", uid));
            _popup.PopupEntity(othersMessage, uid, Robust.Shared.Player.Filter.PvsExcept(uid), true);

            var selfMessage = Loc.GetString("changeling-stasis-enter");
            _popup.PopupEntity(selfMessage, uid, uid);
        }

        if (!_mobState.IsDead(uid))
            _mobState.ChangeMobState(uid, MobState.Dead);

        comp.IsInStasis = true;

        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnExitStasis(EntityUid uid, ChangelingComponent comp, ref ExitStasisEvent args)
    {
        if (!comp.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("changeling-stasis-exit-fail"), uid, uid);
            return;
        }
        if (HasComp<AbsorbedComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-stasis-exit-fail-dead"), uid, uid);
            return;
        }

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        // heal of everything
        _damage.SetAllDamage(uid, 0);
        _mobState.ChangeMobState(uid, MobState.Alive);
        _blood.TryModifyBloodLevel(uid, 1000);
        _blood.TryModifyBleedAmount(uid, -1000);

        _popup.PopupEntity(Loc.GetString("changeling-stasis-exit"), uid, uid);

        comp.IsInStasis = false;

        args.Handled = true;
    }

    #endregion

    [SubscribeLocalEvent]
    private void OnToggleBreathing(EntityUid uid, ChangelingComponent comp, ref ToggleChangelingBreathingEvent args)
    {
        if (args.Handled || !_respirator.SetEnabled(uid, !comp.BreathingEnabled))
            return;

        comp.BreathingEnabled = !comp.BreathingEnabled;
        _actions.SetToggled(args.Action.AsNullable(), !comp.BreathingEnabled);
        Dirty(uid, comp);
        _popup.PopupEntity(Loc.GetString(comp.BreathingEnabled ? "changeling-breathing-enabled" : "changeling-breathing-disabled"), uid, uid);
        args.Handled = true;
    }

    #region Combat Abilities
    [SubscribeLocalEvent]
    private void OnCreateBoneShard(EntityUid uid, ChangelingComponent comp, ref CreateBoneShardEvent args)
    {
        var star = Spawn(BoneShardPrototype, Transform(uid).Coordinates);
        if (!_hands.TryPickupAnyHand(uid, star))
        {
            QueueDel(star);
            _popup.PopupEntity(Loc.GetString("changeling-fail-hands"), uid, uid);
            return;
        }

        PlayMeatySound(uid, comp);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnToggleArmor(EntityUid uid, ChangelingComponent comp, ref ToggleChitinousArmorEvent args)
    {
        if (!TryToggleArmorSet(uid, comp, ArmorPrototype, ArmorHelmetPrototype))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-armor-fail"), uid, uid);
            return;
        }
        PlayMeatySound(uid, comp);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnShriekDissonant(EntityUid uid, ChangelingComponent comp, ref ShriekDissonantEvent args)
    {
        DoScreech(uid, comp);

        var pos = _transform.GetMapCoordinates(uid);
        var power = comp.ShriekPower;
        _emp.EmpPulse(pos, power, 5000f, power * TimeSpan.FromSeconds(2));
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnShriekResonant(EntityUid uid, ChangelingComponent comp, ref ShriekResonantEvent args)
    {
        DoScreech(uid, comp);

        var power = comp.ShriekPower;
        List<EntityUid> ignoreList = new() { uid };
        _flash.FlashArea(uid, uid, power, TimeSpan.FromMilliseconds(power * 2f * 1000f), 0.8f, false, 1f, null, ignoreList);

        var lookup = _lookup.GetEntitiesInRange(uid, power);
        var lights = GetEntityQuery<PoweredLightComponent>();
        foreach (var ent in lookup)
            // breaks lights
            if (lights.HasComponent(ent))
                _light.TryDestroyBulb(ent);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnToggleStrainedMuscles(EntityUid uid, ChangelingComponent comp, ref ToggleStrainedMusclesEvent args)
    {
        ToggleStrainedMuscles(uid, comp);
        args.Handled = true;
    }
    private void ToggleStrainedMuscles(EntityUid uid, ChangelingComponent comp)
    {
        if (!comp.StrainedMusclesActive)
        {
            _popup.PopupEntity(Loc.GetString("changeling-muscles-start"), uid, uid);
            comp.StrainedMusclesActive = true;
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("changeling-muscles-end"), uid, uid);
            comp.StrainedMusclesActive = false;
        }

        _speed.RefreshMovementSpeedModifiers(uid);
        PlayMeatySound(uid, comp);
    }
    [SubscribeLocalEvent]
    private void OnRefreshSpeed(Entity<ChangelingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.StrainedMusclesActive && ent.Comp.Biomass >= 1)
            args.ModifySpeed(1.25f, 1.5f);
    }
    #endregion

    #region Stings

    [SubscribeLocalEvent]
    private void OnStingBlind(EntityUid uid, ChangelingComponent comp, ref StingBlindEvent args)
    {
        if (!TrySting(uid, args))
            return;

        var target = args.Target;
        if (!TryComp<BlindableComponent>(target, out var blindable) || blindable.IsBlind)
            return;

        _blindable.AdjustEyeDamage((target, blindable), 2);
        var timeSpan = TimeSpan.FromSeconds(5f);
        _statusEffect.TryAddStatusEffectDuration(target, "StatusEffectTemporaryBlindness", timeSpan);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnStingChem(Entity<ChangelingComponent> ent, ref StingChemEvent ev)
        => ev.Handled = TryReagentSting(ent.Owner, ent.Comp, ev, ev.Chems);
    [SubscribeLocalEvent]
    private void OnStingTransform(EntityUid uid, ChangelingComponent comp, ref StingTransformEvent args)
    {
        if (!args.Handled && TrySting(uid, args, true))
            args.Handled = TryTransform(args.Target, comp, true, true);
    }
    [SubscribeLocalEvent]
    private void OnStingFakeArmblade(EntityUid uid, ChangelingComponent comp, ref StingFakeArmbladeEvent args)
    {
        if (!TrySting(uid, args))
            return;

        var target = args.Target;
        var fakeArmblade = Spawn(FakeArmbladePrototype, Transform(target).Coordinates);
        if (!_hands.TryPickupAnyHand(target, fakeArmblade))
        {
            QueueDel(fakeArmblade);
            _popup.PopupEntity(Loc.GetString("changeling-sting-fail-simplemob"), uid, uid);
            return;
        }

        PlayMeatySound(target, comp);
        args.Handled = true;
    }
    #endregion

    #region Utilities

    [SubscribeLocalEvent]
    private void OnAnatomicPanacea(EntityUid uid, ChangelingComponent comp, ref ActionAnatomicPanaceaEvent args)
    {
        var reagents = new Dictionary<string, FixedPoint2>
        {
            { "Diphenhydramine", 5f },
            { "Arithrazine", 5f },
            { "Ethylredoxrazine", 5f }
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-panacea"), uid, uid);
        else return;
        PlayMeatySound(uid, comp);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnAugmentedEyesight(EntityUid uid, ChangelingComponent comp, ref ActionAugmentedEyesightEvent args)
    {
        if (HasComp<FlashImmunityComponent>(uid))
        {
            RemComp<FlashImmunityComponent>(uid); // Starlight, they need to be able to disable it to use the other visions due to how flash protection is written.
            _popup.PopupEntity(Loc.GetString("changeling-passive-disable"), uid, uid); // Starlight
            args.Handled = true;
            return;
        }
        // Starlight START
        var flashImmunity = EnsureComp<FlashImmunityComponent>(uid);
        _flashSystem.SetShowInExamine(uid, false, flashImmunity);
        // Starlight END

        _popup.PopupEntity(Loc.GetString("changeling-passive-activate"), uid, uid);
        args.Handled = true;
    }
    #region Starlight
    [SubscribeLocalEvent]
    private void OnNightEyesight(EntityUid uid, ChangelingComponent comp, ref ActionNightEyesightEvent args)
    {
        if (HasComp<NightVisionComponent>(uid))
        {
            RemComp<NightVisionComponent>(uid);
            _popup.PopupEntity(Loc.GetString("changeling-passive-disable"), uid, uid);
            args.Handled = true;
            return;
        }

        EnsureComp<NightVisionComponent>(uid);
        _popup.PopupEntity(Loc.GetString("changeling-passive-activate"), uid, uid);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnThermalEyesight(EntityUid uid, ChangelingComponent comp, ref ActionThermalEyesightEvent args)
    {
        if (HasComp<ThermalVisionComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-passive-active"), uid, uid);
            return;
        }

        EnsureComp<ThermalVisionComponent>(uid);
        _popup.PopupEntity(Loc.GetString("changeling-passive-activate"), uid, uid);
        args.Handled = true;
    }
    #endregion
    /// <summary>
    /// Dissolves cuffs and bolas, frees the changeling from a latch, and splashes
    /// acid on whoever is holding it (latcher, then puller, else its own tile).
    /// </summary>
    [SubscribeLocalEvent]
    private void OnBiodegrade(EntityUid uid, ChangelingComponent comp, ref ActionBiodegradeEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<CuffableComponent>(uid, out var cuffs) && _cuffs.TryGetLastCuff((uid, cuffs), out var cuff))
        {
            _cuffs.Uncuff(uid, uid, cuff.Value);
            QueueDel(cuff.Value);
            args.Handled = true;
        }

        if (TryComp<EnsnareableComponent>(uid, out var ensnareable))
        {
            foreach (var ensnaring in ensnareable.Container.ContainedEntities)
            {
                if (!TryComp<EnsnaringComponent>(ensnaring, out var ensnaringComponent) || !_tag.HasTag(ensnaring, BolaTag))
                    continue;

                _ensnareable.ForceFree(ensnaring, ensnaringComponent);
                QueueDel(ensnaring);
                args.Handled = true;
                break;
            }
        }

        var solution = new Solution();
        solution.AddReagent(PolytrinicAcidPrototype, 10f);
        var target = uid;
        if (TryComp<LatchedComponent>(uid, out var latched) && Exists(latched.Latcher))
        {
            target = latched.Latcher;
            args.Handled |= _latch.TryBreakLatch((uid, latched));
        }
        else if (TryComp<PullableComponent>(uid, out var pullable) && pullable.Puller is { } puller)
        {
            target = puller;
        }
        args.Handled |= _puddle.TrySplashSpillAt(target, Transform(target).Coordinates, solution, out _);
    }

    /// <summary>
    /// Retains the unimplemented legacy last-resort event for compatibility.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnChameleonSkin(EntityUid uid, ChangelingComponent comp, ref ActionChameleonSkinEvent args)
    {
        if (comp.StealthEnabled && HasComp<StealthComponent>(uid) && HasComp<StealthOnMoveComponent>(uid))
            ToggleChameleonSkin(uid, comp, false);
        else
            ToggleChameleonSkin(uid, comp, true);
        args.Handled = true;
    }
    public void ToggleChameleonSkin(EntityUid uid, ChangelingComponent comp, bool toState)
    {
        if (!toState)
        {
            RemComp<StealthComponent>(uid);
            RemComp<StealthOnMoveComponent>(uid);
            _popup.PopupEntity(Loc.GetString("changeling-chameleon-end"), uid, uid);
            comp.StealthEnabled = false;
        }
        else
        {
            EnsureComp<StealthComponent>(uid);
            var stealthonmove = EnsureComp<StealthOnMoveComponent>(uid);
            stealthonmove.PassiveVisibilityRate = -0.37f;
            _popup.PopupEntity(Loc.GetString("changeling-chameleon-start"), uid, uid);
            comp.StealthEnabled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnEphedrineOverdose(EntityUid uid, ChangelingComponent comp, ref ActionEphedrineOverdoseEvent args)
    {
        var stam = EnsureComp<StaminaComponent>(uid);
        stam.StaminaDamage = 0;

        var reagents = new Dictionary<string, FixedPoint2>
        {
            { "Ephedrine", 15f }
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-inject"), uid, uid);
        else
        {
            _popup.PopupEntity(Loc.GetString("changeling-inject-fail"), uid, uid);
            return;
        }
        args.Handled = true;
    }
    #region Starlight
    [SubscribeLocalEvent]
    private void OnDesoxyephedrineOverdose(EntityUid uid, ChangelingComponent comp, ref ActionDesoxyephedrineOverdoseEvent args)
    {
        var stam = EnsureComp<StaminaComponent>(uid);
        stam.StaminaDamage = 0;

        var reagents = new Dictionary<string, FixedPoint2>
        {
            { "Desoxyephedrine", 15f }
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-inject"), uid, uid);
        else
        {
            _popup.PopupEntity(Loc.GetString("changeling-inject-fail"), uid, uid);
            return;
        }
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnAmalgamOverdose(EntityUid uid, ChangelingComponent comp, ref ActionAmalgamOverdoseEvent args)
    {
        var stam = EnsureComp<StaminaComponent>(uid);
        stam.StaminaDamage = 0;

        var reagents = new Dictionary<string, FixedPoint2>
        {
            { "Amalgam", 15f }
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-inject"), uid, uid);
        else
        {
            _popup.PopupEntity(Loc.GetString("changeling-inject-fail"), uid, uid);
            return;
        }
        args.Handled = true;
    }
    #endregion

    // john space made me do this
    [SubscribeLocalEvent]
    private void OnHealUltraSwag(EntityUid uid, ChangelingComponent comp, ref ActionFleshmendEvent args)
    {
        var reagents = new Dictionary<string, FixedPoint2>
        {
            { "Ichor", 10f },
            { "TranexamicAcid", 5f }
        };
        if (TryInjectReagents(uid, reagents))
            _popup.PopupEntity(Loc.GetString("changeling-fleshmend"), uid, uid);
        else return;
        PlayMeatySound(uid, comp);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    public void OnLastResort(EntityUid uid, ChangelingComponent comp, ref ActionLastResortEvent args)
    {
        // todo: implement
    }
    [SubscribeLocalEvent]
    public void OnLesserForm(EntityUid uid, ChangelingComponent comp, ref ActionLesserFormEvent args)
    {
        if (args.Handled || TransformEntity(uid, protoId: "MobLingMonkey", comp: comp) is not { } monkey)
            return;

        var transformed = Comp<ChangelingComponent>(monkey);
        // This replaces a carrier, rather than creating an independent one. The successful action's cost follows it.
        transformed.Resources = comp.Resources;
        transformed.IsInLesserForm = true;
        Dirty(monkey, transformed);
        PlayMeatySound(monkey, transformed);
        var message = Loc.GetString("changeling-transform-others", ("user", Identity.Entity(monkey, EntityManager)));
        _popup.PopupEntity(message, monkey, PopupType.LargeCaution);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    private void OnLingActionPerformed(Entity<ChangelingActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (!TryComp<ChangelingComponent>(args.Performer, out var comp))
            return;

        var chemicalCost = ent.Comp.ChemicalCost;
        if (chemicalCost != 0)
            UpdateChemicals(args.Performer, comp, -chemicalCost);
        if (ent.Comp.BiomassCost != 0)
            UpdateBiomass(args.Performer, comp, -ent.Comp.BiomassCost);

    }

    private bool TryToggleArmorSet(EntityUid uid, ChangelingComponent comp, EntProtoId armor, EntProtoId helmet)
    {
        if (comp.Equipment.ContainsKey(armor.Id) || comp.Equipment.ContainsKey(helmet.Id))
        {
            if (comp.Equipment.ContainsKey(armor.Id))
                TryToggleItem(uid, armor, comp, "outerClothing");
            if (comp.Equipment.ContainsKey(helmet.Id))
                TryToggleItem(uid, helmet, comp, "head");
            return true;
        }

        if (!TryToggleItem(uid, armor, comp, "outerClothing"))
            return false;
        if (TryToggleItem(uid, helmet, comp, "head"))
            return true;

        TryToggleItem(uid, armor, comp, "outerClothing");
        return false;
    }

    [SubscribeLocalEvent]
    public void OnSpacesuit(EntityUid uid, ChangelingComponent comp, ref ActionSpacesuitEvent args)
    {
        var armor = SpacesuitPrototype;
        var helmet = SpacesuitHelmetPrototype;
        if (!TryToggleArmorSet(uid, comp, armor, helmet))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-armor-fail"), uid, uid);
            return;
        }

        PlayMeatySound(uid, comp);
        args.Handled = true;
    }
    #region Starlight
    [SubscribeLocalEvent]
    public void OnNeocyteDisguise(EntityUid uid, ChangelingComponent comp, ref ActionNeocyteDisguiseEvent args)
    {
        if (!TryToggleItem(uid, NeocyteDisguisePrototype, comp, "outerClothing2"))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-neocyte-fail"), uid, uid);
            return;
        }

        PlayMeatySound(uid, comp);
        args.Handled = true;
    }
    #endregion
    [SubscribeLocalEvent]
    public void OnHivemindAccess(EntityUid uid, ChangelingComponent comp, ref ActionHivemindAccessEvent args)
    {
        if (HasComp<HivemindComponent>(uid))
        {
            _popup.PopupEntity(Loc.GetString("changeling-passive-active"), uid, uid);
            return;
        }

        EnsureComp<HivemindComponent>(uid);

        _language.AddLanguage(uid, "Changeling");

        _popup.PopupEntity(Loc.GetString("changeling-hivemind-start"), uid, uid);
        args.Handled = true;
    }
    [SubscribeLocalEvent]
    public void OnFakeMindShieldToggle(EntityUid uid, ChangelingComponent comp, FakeMindShieldToggleEvent toggleEvent) => EnsureComp<FakeMindShieldComponent>(uid);
    #endregion

    [SubscribeLocalEvent(before: [typeof(RetractableItemActionSystem)])]
    private void OnAttemptLingAction(Entity<ChangelingActionComponent> ent, ref ActionAttemptEvent ev)
    {
        if (ev.Cancelled) return;
        var uid = ev.User;
        if (!TryComp<ChangelingComponent>(uid, out var comp))
        {
            ev.Cancelled = true;
            return;
        }

        var lingAction = ent.Comp;

        if (comp.Biomass < 1 && lingAction.RequireBiomass)
        {
            _popup.PopupEntity(Loc.GetString("changeling-biomass-deficit"), uid, uid);
            ev.Cancelled = true;
            return;
        }

        if (!lingAction.UseInLesserForm && comp.IsInLesserForm)
        {
            _popup.PopupEntity(Loc.GetString("changeling-action-fail-lesserform"), uid, uid);
            ev.Cancelled = true;
            return;
        }

        if (comp.Chemicals < lingAction.ChemicalCost)
        {
            _popup.PopupEntity(Loc.GetString("changeling-chemicals-deficit"), uid, uid);
            ev.Cancelled = true;
            return;
        }

    }
}
