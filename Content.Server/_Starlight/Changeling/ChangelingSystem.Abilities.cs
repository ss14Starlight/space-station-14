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
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Changeling;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring.Components;
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
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private StomachSystem _stomach = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ChangelingEvolutionSystem _evolution = default!;

    private const float StrengthChemicalDrain = 3f;

    private static readonly ProtoId<ReagentPrototype> FerrochromicAcidPrototype = "FerrochromicAcid";

    public void SubscribeAbilities()
    {
        SubscribeLocalEvent<ChangelingComponent, OpenEvolutionMenuEvent>(OnOpenEvolutionMenu);
        SubscribeLocalEvent<ChangelingComponent, AbsorbDNAEvent>(OnAbsorb);
        SubscribeLocalEvent<ChangelingComponent, ChangelingDevourFinishedEvent>(OnDevourEvolutionReward);
        SubscribeLocalEvent<ChangelingComponent, OnLingDevour>(OnDevouredPerson);
        SubscribeLocalEvent<ChangelingComponent, StingExtractDNAEvent>(OnStingExtractDNA);
        SubscribeLocalEvent<ChangelingComponent, EnterStasisEvent>(OnEnterStasis);
        SubscribeLocalEvent<ChangelingComponent, ExitStasisEvent>(OnExitStasis);
        SubscribeLocalEvent<ChangelingComponent, ToggleChangelingBreathingEvent>(OnToggleBreathing);

        SubscribeLocalEvent<ChangelingComponent, CreateBoneShardEvent>(OnCreateBoneShard);
        SubscribeLocalEvent<ChangelingComponent, ToggleChitinousArmorEvent>(OnToggleArmor);
        SubscribeLocalEvent<ChangelingComponent, ShriekDissonantEvent>(OnShriekDissonant);
        SubscribeLocalEvent<ChangelingComponent, ShriekResonantEvent>(OnShriekResonant);
        SubscribeLocalEvent<ChangelingComponent, ToggleStrainedMusclesEvent>(OnToggleStrainedMuscles);
        SubscribeLocalEvent<ChangelingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);

        SubscribeChemistry();
        SubscribeStings();
        SubscribeLegacyAbilities();
        InitializeEvolution();

        SubscribeLocalEvent<ChangelingComponent, ActionAnatomicPanaceaEvent>(OnAnatomicPanacea);
        SubscribeLocalEvent<ChangelingComponent, ActionAugmentedEyesightEvent>(OnAugmentedEyesight);
        SubscribeLocalEvent<ChangelingComponent, ActionNightEyesightEvent>(OnNightEyesight); // Starlight
        SubscribeLocalEvent<ChangelingComponent, ActionThermalEyesightEvent>(OnThermalEyesight); // Starlight
        SubscribeLocalEvent<ChangelingComponent, ActionChameleonSkinEvent>(OnChameleonSkin);
        SubscribeLocalEvent<ChangelingComponent, ActionEphedrineOverdoseEvent>(OnEphedrineOverdose);
        SubscribeLocalEvent<ChangelingComponent, ActionDesoxyephedrineOverdoseEvent>(OnDesoxyephedrineOverdose); // Starlight
        SubscribeLocalEvent<ChangelingComponent, ActionAmalgamOverdoseEvent>(OnAmalgamOverdose); // Starlight
        SubscribeLocalEvent<ChangelingComponent, ActionFleshmendEvent>(OnHealUltraSwag);
        SubscribeLocalEvent<ChangelingComponent, ActionSpacesuitEvent>(OnSpacesuit);
        SubscribeLocalEvent<ChangelingComponent, ActionNeocyteDisguiseEvent>(OnNeocyteDisguise); // Starlight
        SubscribeLocalEvent<ChangelingComponent, ActionHivemindAccessEvent>(OnHivemindAccess);
        SubscribeLocalEvent<ChangelingComponent, FakeMindShieldToggleEvent>(OnFakeMindShieldToggle);

        SubscribeLocalEvent<ChangelingActionComponent, ActionAttemptEvent>(OnAttemptLingAction, before: [typeof(RetractableItemActionSystem)]);
        SubscribeLocalEvent<ChangelingActionComponent, ActionPerformedEvent>(OnLingActionPerformed);
    }

    #region Basic Abilities

    private void OnOpenEvolutionMenu(EntityUid uid, ChangelingComponent comp, ref OpenEvolutionMenuEvent args)
    {
        if (TryGetColony(uid, out var member, out _) && !IsHiveMaster(uid, member))
            return;
        if (!TryComp<StoreComponent>(uid, out var store))
            return;

        _store.ToggleUi(uid, uid, store);
        args.Handled = true;
    }

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

    private void OnDevourEvolutionReward(EntityUid uid, ChangelingComponent comp, ref ChangelingDevourFinishedEvent args)
    {
        if (!args.Completed || args.Handled || HasComp<ChangelingInfectionComponent>(uid)
            || TryComp<ChangelingColonyMemberComponent>(uid, out var member) && member.IsFragment)
            return;
        AwardEvolutionPoints(uid, args.Target);
    }

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

        var owner = TryGetColony(uid, out var member, out _) ? member.Mind : _mind.GetMind(uid);
        if (owner is { } mindId && TryComp<Content.Shared.Mind.MindComponent>(mindId, out var mind))
            if (_mind.TryGetObjectiveComp<AbsorbConditionComponent>(mindId, out var objective, mind))
                objective.Absorbed += 1;
        AbsorbHiveMind(uid, target);
    }

    private void AwardEvolutionPoints(EntityUid uid, EntityUid target)
    {
        if (HasComp<AbsorbedComponent>(target))
            return;
        var points = HasComp<ChangelingComponent>(target) ? 10 : HasComp<HumanoidAppearanceComponent>(target) ? 2 : 0;
        if (points == 0)
            return;
        var owner = TryGetColony(uid, out _, out var colony) && colony.Core.IsValid() ? colony.Core : uid;
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
        var stores = EntityQueryEnumerator<StoreComponent>();
        while (stores.MoveNext(out var body, out var sharedStore))
        {
            if (ReferenceEquals(sharedStore.Balance, store.Balance) && _chemistryUi.IsUiOpen(body, StoreUiKey.Key))
                _store.UpdateUserInterface(body, body, sharedStore);
        }
    }

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

    #region Combat Abilities
    private void OnCreateBoneShard(EntityUid uid, ChangelingComponent comp, ref CreateBoneShardEvent args)
    {
        var star = Spawn(comp.Evolved ? EvolvedBoneShardPrototype : BoneShardPrototype, Transform(uid).Coordinates);
        if (!_hands.TryPickupAnyHand(uid, star))
        {
            QueueDel(star);
            _popup.PopupEntity(Loc.GetString("changeling-fail-hands"), uid, uid);
            return;
        }

        PlayMeatySound(uid, comp);
        ShowAbilityFeedback(uid, args.Action);
        args.Handled = true;
    }
    private void OnToggleArmor(EntityUid uid, ChangelingComponent comp, ref ToggleChitinousArmorEvent args)
    {
        if (!TryToggleArmorSet(uid, comp, ArmorPrototype, ArmorHelmetPrototype))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-armor-fail"), uid, uid);
            return;
        }

        PlayMeatySound(uid, comp);
        ShowAbilityFeedback(uid, args.Action, comp.Equipment.ContainsKey(ArmorPrototype.Id));
        args.Handled = true;
    }
    private void OnShriekDissonant(EntityUid uid, ChangelingComponent comp, ref ShriekDissonantEvent args)
    {
        DoScreech(uid, comp);

        var pos = _transform.GetMapCoordinates(uid);
        var power = comp.ShriekPower;
        _emp.EmpPulse(pos, power, 5000f, power * TimeSpan.FromSeconds(2));
        if (comp.Evolved)
            ApplyResonantShriek(uid, comp);
        ShowAbilityFeedback(uid, args.Action);
        args.Handled = true;
    }
    private void OnShriekResonant(EntityUid uid, ChangelingComponent comp, ref ShriekResonantEvent args)
    {
        DoScreech(uid, comp);
        ApplyResonantShriek(uid, comp);
        ShowAbilityFeedback(uid, args.Action);
        args.Handled = true;
    }

    private void ApplyResonantShriek(EntityUid uid, ChangelingComponent comp)
    {
        var power = comp.ShriekPower;
        List<EntityUid> ignoreList = new() { uid };
        _flash.FlashArea(uid, uid, power, TimeSpan.FromMilliseconds(power * 2f * 1000f), 0.8f, false, 1f, null, ignoreList);

        var lookup = _lookup.GetEntitiesInRange(uid, power);
        var lights = GetEntityQuery<PoweredLightComponent>();
        foreach (var ent in lookup)
            // breaks lights
            if (lights.HasComponent(ent))
                _light.TryDestroyBulb(ent);
    }
    private void OnToggleStrainedMuscles(EntityUid uid, ChangelingComponent comp, ref ToggleStrainedMusclesEvent args)
    {
        if (args.Handled)
            return;
        if (!comp.StrainedMusclesActive)
        {
            if (!CanActivateAdaptation(uid, comp))
                return;
            if (comp.Chemicals < StrengthChemicalDrain * (float) comp.ChemicalUpdateCooldown.TotalSeconds)
            {
                _popup.PopupEntity(Loc.GetString("changeling-chemicals-deficit"), uid, uid);
                return;
            }
        }
        ToggleStrainedMuscles(uid, comp);
        args.Handled = true;
    }

    private void ToggleStrainedMuscles(EntityUid uid, ChangelingComponent comp)
    {
        if (!comp.StrainedMusclesActive)
        {
            _popup.PopupEntity(Loc.GetString("changeling-strength-enabled"), uid, uid);
            comp.StrainedMusclesActive = true;
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("changeling-strength-disabled"), uid, uid);
            comp.StrainedMusclesActive = false;
        }

        _evolution.RefreshStrength((uid, comp));
        Dirty(uid, comp);
        _speed.RefreshMovementSpeedModifiers(uid);
        SetAbilityToggled<ToggleStrainedMusclesEvent>(uid, comp.StrainedMusclesActive);
        PlayMeatySound(uid, comp);
    }
    private void OnRefreshSpeed(Entity<ChangelingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.StrainedMusclesActive && ent.Comp.Biomass >= 1)
            args.ModifySpeed(ent.Comp.Evolved ? 1.5f : 1.25f, ent.Comp.Evolved ? 1.75f : 1.5f);
        else
            args.ModifySpeed(1f, 1f);
    }

    #endregion

    #region Utilities

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

    private void OnAugmentedEyesight(EntityUid uid, ChangelingComponent comp, ref ActionAugmentedEyesightEvent args)
        => ToggleEnhancedEyes(uid, comp, ref args);
    #region Starlight
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
            ConfigureChameleon(uid, comp);
            _popup.PopupEntity(Loc.GetString("changeling-chameleon-start"), uid, uid);
            comp.StealthEnabled = true;
        }
        SetAbilityToggled<ActionChameleonSkinEvent>(uid, comp.StealthEnabled);
    }

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
    public void OnSpacesuit(EntityUid uid, ChangelingComponent comp, ref ActionSpacesuitEvent args)
    {
        var armor = comp.Evolved ? ArmorPrototype : SpacesuitPrototype;
        var helmet = comp.Evolved ? ArmorHelmetPrototype : SpacesuitHelmetPrototype;
        if (!TryToggleArmorSet(uid, comp, armor, helmet))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-armor-fail"), uid, uid);
            return;
        }

        PlayMeatySound(uid, comp);
        ShowAbilityFeedback(uid, args.Action, comp.Equipment.ContainsKey(armor.Id));
        args.Handled = true;
    }
    #region Starlight
    public void OnNeocyteDisguise(EntityUid uid, ChangelingComponent comp, ref ActionNeocyteDisguiseEvent args)
    {
        if (!TryToggleItem(uid, NeocyteDisguisePrototype, comp, "outerClothing2"))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-neocyte-fail"), uid, uid);
            return;
        }

        PlayMeatySound(uid, comp);
        ShowAbilityFeedback(uid, args.Action, comp.Equipment.ContainsKey(NeocyteDisguisePrototype.Id));
        args.Handled = true;
    }
    #endregion
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

    public void OnFakeMindShieldToggle(EntityUid uid, ChangelingComponent comp, FakeMindShieldToggleEvent toggleEvent) => EnsureComp<FakeMindShieldComponent>(uid);
    #endregion

    private void OnAttemptLingAction(Entity<ChangelingActionComponent> ent, ref ActionAttemptEvent ev)
    {
        if (ev.Cancelled || !IsFragmentActionAllowed(ev.User, ent.Owner))
        {
            ev.Cancelled = true;
            return;
        }
        var uid = ev.User;
        if (!TryComp<ChangelingComponent>(uid, out var comp))
        {
            ev.Cancelled = true;
            return;
        }

        var lingAction = ent.Comp;
        ApplyActionEvolution(comp, ent.Owner);
        var chemicalCost = ChemicalCost(comp, lingAction);

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

        if (comp.Chemicals < chemicalCost)
        {
            _popup.PopupEntity(Loc.GetString("changeling-chemicals-deficit"), uid, uid);
            ev.Cancelled = true;
            return;
        }
    }

    private void OnLingActionPerformed(Entity<ChangelingActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (!TryComp<ChangelingComponent>(args.Performer, out var comp))
            return;

        var chemicalCost = ChemicalCost(comp, ent.Comp);
        if (chemicalCost != 0)
            UpdateChemicals(args.Performer, comp, -chemicalCost);
        if (ent.Comp.BiomassCost != 0)
            UpdateBiomass(args.Performer, comp, -ent.Comp.BiomassCost);

        if (_actions.GetEvent(ent.Owner) is OnRetractableItemActionEvent
            && TryComp<RetractableItemActionComponent>(ent.Owner, out var retractable))
            ShowAbilityFeedback(args.Performer, ent.Owner, _hands.IsHolding(args.Performer, retractable.ActionItemUid));
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

    private void SetAbilityToggled<TEvent>(EntityUid uid, bool toggled) where TEvent : BaseActionEvent
    {
        foreach (var action in _actions.GetActions(uid))
            if (_actions.GetEvent(action.Owner) is TEvent)
                _actions.SetToggled(action.Owner, toggled);
    }

    private void ShowAbilityFeedback(EntityUid uid, EntityUid action, bool? enabled = null)
    {
        if (enabled is { } toggled)
            _actions.SetToggled(action, toggled);
        var message = enabled switch
        {
            true => "changeling-ability-enabled",
            false => "changeling-ability-disabled",
            null => "changeling-ability-activated",
        };
        _popup.PopupEntity(Loc.GetString(message, ("ability", Name(action))), uid, uid);
    }
}
