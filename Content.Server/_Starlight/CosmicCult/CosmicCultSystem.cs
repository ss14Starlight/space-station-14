using Content.Server.Actions;
using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Events;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.CosmicCult;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.CosmicCult;

public sealed partial class CosmicCultSystem : SharedCosmicCultSystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private CosmicCultRuleSystem _cultRule = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ServerGlobalSoundSystem _sound = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private Shared.StatusEffectNew.StatusEffectsSystem _statusEffects = default!;

    private readonly ResPath _mapPath = new("Maps/_Starlight/Other/cosmicvoid.yml");

    private static readonly EntProtoId _cosmicEchoVfx = "CosmicEchoVfx";
    private static readonly EntProtoId _entropicDegen = "EntropicDegen";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<CosmicCultVoterEligibilityEvent>(OnCheckEligibility);

        SubscribeLocalEvent<CosmicCultComponent, ComponentInit>(OnStartCultist);
        SubscribeLocalEvent<CosmicCultLeadComponent, ComponentInit>(OnStartCultLead);
        SubscribeLocalEvent<CosmicCultLeadComponent, ComponentShutdown>(OnEndCultLead);
        SubscribeLocalEvent<CosmicCultComponent, GetVisMaskEvent>(OnGetVisMask);

        SubscribeLocalEvent<CosmicEquipmentComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<CosmicEquipmentComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<CosmicEquipmentComponent, GotEquippedHandEvent>(OnGotHeld);
        SubscribeLocalEvent<CosmicEquipmentComponent, GotUnequippedHandEvent>(OnGotUnheld);

        SubscribeLocalEvent<InfluenceStrideComponent, ComponentInit>(OnStartInfluenceStride);
        SubscribeLocalEvent<InfluenceStrideComponent, ComponentRemove>(OnEndInfluenceStride);
        SubscribeLocalEvent<InfluenceStrideComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<CosmicImposingComponent, ComponentInit>(OnStartImposition);
        SubscribeLocalEvent<CosmicImposingComponent, ComponentRemove>(OnEndImposition);
        SubscribeLocalEvent<CosmicImposingComponent, RefreshMovementSpeedModifiersEvent>(OnImpositionMoveSpeed);

        SubscribeLocalEvent<CosmicCultExamineComponent, ExaminedEvent>(OnCosmicCultExamined);

        SubscribeFinale(); //Hook up the cosmic cult finale system
    }

    public void MalignEcho(Entity<CosmicCultComponent> uid)
    {
        if (_cultRule.AssociatedGamerule(uid) is not { } cult)
            return;
        if (cult.Comp.CurrentTier > 1 && !_random.Prob(0.5f))
            Spawn(_cosmicEchoVfx, Transform(uid).Coordinates);
    }

    #region Housekeeping

    // Rogue Ascendants use this too, which are generalized MidRoundAntags, so we keep the map around. If you're porting cosmic cult, and do not want rogue ascendants, feel free to move this into selective usage akin to NukeOps base.
    /// <summary>
    /// Creates the Cosmic Void pocket dimension map.
    /// </summary>
    private void OnRoundStart(RoundStartingEvent ev)
    {
        if (_mapLoader.TryLoadMap(_mapPath, out var map, out _, new DeserializationOptions { InitializeMaps = true }))
            _map.SetPaused(map.Value.Comp.MapId, false);
    }

    private void OnCheckEligibility(ref CosmicCultVoterEligibilityEvent args)
        => args.Eligible = HasComp<CosmicCultComponent>(args.Player.AttachedEntity);

    private void OnCosmicCultExamined(Entity<CosmicCultExamineComponent> ent, ref ExaminedEvent args)
        => args.PushMarkup(Loc.GetString(EntitySeesCult(args.Examiner) ? ent.Comp.CultistText : ent.Comp.OthersText));
    #endregion

    #region Init Cult
    /// <summary>
    /// Add the starting powers to the cultist.
    /// </summary>
    private void OnStartCultist(Entity<CosmicCultComponent> uid, ref ComponentInit args)
    {
        foreach (var actionId in uid.Comp.CosmicCultActions)
        {
            var actionEnt = _actions.AddAction(uid, actionId);
            uid.Comp.ActionEntities.Add(actionEnt);
        }
        _eye.RefreshVisibilityMask(uid.Owner);
        _alerts.ShowAlert(uid.Owner, uid.Comp.EntropyAlert);
    }

    /// <summary>
    /// Add the Monument summon action to the cult lead.
    /// </summary>
    private void OnStartCultLead(Entity<CosmicCultLeadComponent> uid, ref ComponentInit args)
        => _actions.AddAction(uid.Owner, ref uid.Comp.CosmicMonumentPlaceActionEntity, uid.Comp.CosmicMonumentPlaceAction, uid);

    private void OnEndCultLead(Entity<CosmicCultLeadComponent> uid, ref ComponentShutdown args)
    {
        if (uid.Comp.CosmicMonumentPlaceActionEntity is { } placeAction && Exists(placeAction))
            _actions.RemoveAction(placeAction);

        if (uid.Comp.CosmicMonumentMoveActionEntity is { } moveAction && Exists(moveAction))
            _actions.RemoveAction(moveAction);

        uid.Comp.CosmicMonumentPlaceActionEntity = null;
        uid.Comp.CosmicMonumentMoveActionEntity = null;
    }

    private void OnGetVisMask(Entity<CosmicCultComponent> uid, ref GetVisMaskEvent args)
        => args.VisibilityMask |= (int)VisibilityFlags.NullSpace;

    /// <summary>
    /// Called by Cosmic Siphon. Increments the Cult's global objective tracker.
    /// </summary>
    #endregion

    #region Equipment Pickup
    private void OnGotEquipped(Entity<CosmicEquipmentComponent> ent, ref GotEquippedEvent args)
    {
        if (!EntityIsCultist(args.Equipee))
            _statusEffects.TrySetStatusEffectDuration(args.Equipee, _entropicDegen, out _);
    }

    private void OnGotUnequipped(Entity<CosmicEquipmentComponent> ent, ref GotUnequippedEvent args)
    {
        if (!EntityIsCultist(args.Equipee))
            _statusEffects.TryRemoveStatusEffect(args.Equipee, _entropicDegen);
    }
    private void OnGotHeld(Entity<CosmicEquipmentComponent> ent, ref GotEquippedHandEvent args)
    {
        if (!EntityIsCultist(args.User))
        {
            _statusEffects.TrySetStatusEffectDuration(args.User, _entropicDegen, out _);
            _popup.PopupEntity(Loc.GetString("cosmiccult-gear-pickup", ("ITEM", args.Equipped)), args.User, args.User, PopupType.MediumCaution);
        }
    }

    private void OnGotUnheld(Entity<CosmicEquipmentComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!EntityIsCultist(args.User))
            _statusEffects.TryRemoveStatusEffect(args.User, _entropicDegen);
    }
    #endregion

    #region Movespeed
    private void OnStartInfluenceStride(Entity<InfluenceStrideComponent> uid, ref ComponentInit args) => // i wish movespeed was easier to work with
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

    private void OnEndInfluenceStride(Entity<InfluenceStrideComponent> uid, ref ComponentRemove args) => // that movespeed applies more-or-less correctly
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

    private void OnStartImposition(Entity<CosmicImposingComponent> uid, ref ComponentInit args) // these functions just make sure
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        EnsureComp<CosmicCultExamineComponent>(uid).CultistText = "cosmic-examine-text-malignecho";
    }
    private void OnEndImposition(Entity<CosmicImposingComponent> uid, ref ComponentRemove args)
    { // as various cosmic cult effects get added and removed
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        RemComp<CosmicCultExamineComponent>(uid);
    }
    private void OnRefreshMoveSpeed(EntityUid uid, InfluenceStrideComponent comp, RefreshMovementSpeedModifiersEvent args) =>
        args.ModifySpeed(comp.StrideSpeedMultiplier, comp.StrideSpeedMultiplier);

    private void OnImpositionMoveSpeed(EntityUid uid, CosmicImposingComponent comp, RefreshMovementSpeedModifiersEvent args) =>
        args.ModifySpeed(comp.SpeedMultiplier, comp.SpeedMultiplier);
    #endregion
}
