using Content.Shared.Humanoid;
using Content.Shared.Alert;
using System.Linq;
using Content.Shared._Starlight.Bluespace;
using Content.Shared.Examine;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Damage;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Station;
using Content.Shared.Popups;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Random;
using Content.Shared.Damage.Systems;
using Content.Shared.Ensnaring;
using Robust.Shared.Audio.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map.Components;
using Content.Shared._Starlight.Medical.Body.Events;
using Robust.Shared.Containers;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared._Starlight.NullSpace.Components;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared._Starlight.NullSpace.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Light;

namespace Content.Shared._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NullSpacePhaseSystem _nullspace = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedEnsnareableSystem _ensnareable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedGameTicker _gameTicker = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedLanguageSystem _language = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly LightLevelSystem _lightLevel = default!;

    [Dependency] private readonly EntityQuery<DarkLightComponent> _darkLightQuery = default!;
    [Dependency] private readonly EntityQuery<ShadegenAffectedComponent> _shadegenAffected = default!;

    private static readonly ProtoId<TagPrototype> _theDarkTag = "TheDark";
    private static readonly ProtoId<TagPrototype> _coreTag = "ShadekinCore";
    private static readonly ProtoId<TagPrototype> _damagedCoreTag = "DamagedShadekinCore";
    private static readonly ProtoId<DamageTypePrototype> _heatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> _cellularType = "Cellular";
    private static readonly EntProtoId<GameRuleComponent> _theDarkMap = "TheDarkMap";
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateCooldown = TimeSpan.FromSeconds(1f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadekinComponent, ComponentShutdown>((ent, ref _) =>
        {
            if (_timing.ApplyingState)
                return;

            RemComp<BrighteyeComponent>(ent);
        });
        SubscribeLocalEvent<ShadekinComponent, BeforeDamageChangedEvent>((_, ref args) => args.Damage.DamageDict["Asphyxiation"] = 0);

        InitializeBrighteye();
        InitializeAbilities();
    }

    [SubscribeLocalEvent]
    private void CoreOrganInit(Entity<OrganShadekinCoreComponent> ent, ref OrganAddedToBodyEvent args)
        => ent.Comp.OrganOwner ??= args.Body;

    [SubscribeLocalEvent]
    private void OnExamined(Entity<OrganShadekinCoreComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Damaged)
            args.PushMarkup(Loc.GetString("shadekin-core-undamaged"));

        if (ent.Comp.OrganOwner == args.Examiner)
            args.PushMarkup(Loc.GetString("shadekin-core-owner"));
    }

    [SubscribeLocalEvent]
    private void OnEyeColorChange(Entity<ShadekinComponent> ent, ref EyeColorInitEvent _)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.EyeGlowing = false;
        Dirty(ent.Owner, humanoid);
    }

    [SubscribeLocalEvent]
    private void NullSpaceShunt(Entity<ShadekinComponent> ent, ref NullSpaceShuntEvent __)
    {
        if (TryComp<BodyComponent>(ent.Owner, out var body)
            && _bodySystem.TryGetOrgansWithComponent<OrganShadekinCoreComponent>((ent.Owner, body), out _))
        {
            _popup.PopupPredicted(Loc.GetString("shadekin-shunt"), ent.Owner, ent.Owner, PopupType.LargeCaution);

            _stunSystem.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(1), autoStand: false);
            ApplyCoreDamage(ent.Owner, 5);
        }
    }

    public void UpdateAlert(EntityUid uid, ShadekinComponent component, short state)
        => _alerts.ShowAlert(uid, component.ShadekinAlert, state);

    private void SetPassiveBuff(EntityUid uid, ShadekinState shadekinState)
    {
        if (!TryComp<PassiveDamageComponent>(uid, out var passive))
            return;

        if (shadekinState is ShadekinState.Annoying or
            ShadekinState.High or
            ShadekinState.Extreme)
        {
            passive.DamageCap = 1;
        }
        else if (shadekinState == ShadekinState.Low)
        {
            passive.DamageCap = 20;
            passive.AllowedStates.Clear();
            passive.AllowedStates.Add(MobState.Alive);
            passive.Interval = 1f;
        }
        else if (shadekinState == ShadekinState.Dark)
        {
            passive.DamageCap = 0;
            passive.AllowedStates.Clear();
            passive.AllowedStates.Add(MobState.Alive);
            passive.AllowedStates.Add(MobState.Critical);
            passive.AllowedStates.Add(MobState.Dead);
            passive.Interval = 0.5f;
        }
    }

    private void ApplyLightDamage(EntityUid uid, float dmg)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add(_heatType, dmg);
        _damageable.TryChangeDamage(uid, damage, true, false);
    }

    private void ApplyCoreDamage(EntityUid uid, float dmg)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add(_cellularType, dmg);
        _damageable.TryChangeDamage(uid, damage, false, false);
    }

    [SubscribeLocalEvent]
    private void OnRefreshMovementSpeedModifiers(Entity<ShadekinComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.CurrentState is ShadekinState.High or ShadekinState.Extreme)
        {
            if (!TryComp<MovementSpeedModifierComponent>(ent, out var movement))
                return;

            var sprintDif = movement.BaseWalkSpeed / movement.BaseSprintSpeed;
            args.ModifySpeed(1f, sprintDif);
        }
    }

    private void ToggleNightVision(EntityUid uid, ShadekinState shadekinState)
    {
        var nightVision = EnsureComp<NightVisionComponent>(uid);
        var shouldBeActive = shadekinState == ShadekinState.Dark;

        if (nightVision.Active == shouldBeActive)
            return;

        nightVision.Active = shouldBeActive;

        Dirty(uid, nightVision);
    }

    private void CheckThresholds(EntityUid uid, ShadekinComponent component, float lightExposure)
    {
        foreach (var (threshold, shadekinState) in component.Thresholds.Reverse())
        {
            var selectedstate = shadekinState;
            if (lightExposure < threshold)
            {
                if (selectedstate == ShadekinState.Low)
                    selectedstate = ShadekinState.Dark;
                else
                    continue;
            }

            component.CurrentState = selectedstate;
            UpdateAlert(uid, component, (short)selectedstate);
            Dirty(uid, component);
            break;
        }
    }

    public bool AreWeInTheDark(EntityUid uid)
        => Transform(uid).MapUid is { } mapUid && _tag.HasTag(mapUid, _theDarkTag);

    public void SpawnTheDark()
    {
        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var mapuid, out var mapcomp))
        {
            if (!mapcomp.MapPaused
                && _tag.HasTag(mapuid, _theDarkTag))
                return;
        }
        _gameTicker.StartGameRule(_theDarkMap);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ShadekinComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextUpdate)
                continue;

            component.NextUpdate = _timing.CurTime + component.UpdateCooldown;

            var lightExposure = 0f;

            if (!HasComp<NullSpaceComponent>(uid) && !AreWeInTheDark(uid))
                lightExposure = _lightLevel.CalculateLightLevel(uid);

            CheckThresholds(uid, component, lightExposure);

            ToggleNightVision(uid, component.CurrentState);
            SetPassiveBuff(uid, component.CurrentState);
            _speed.RefreshMovementSpeedModifiers(uid);

            if (component.CurrentState == ShadekinState.Extreme)
                ApplyLightDamage(uid, 1);

            if (TryComp<BrighteyeComponent>(uid, out var brighteye))
                UpdateEnergy(uid, component, brighteye);
        }

        // The Dark Effects - This only applies for Ents that are IN THE DARK.
        if (_timing.CurTime > _nextUpdate)
        {
            _nextUpdate = _timing.CurTime + _updateCooldown;

            var thedarkmobquery = EntityQueryEnumerator<MobStateComponent>();
            while (thedarkmobquery.MoveNext(out var uid, out var _))
            {
                var remove = false;

                if (_status.HasStatusEffect(uid, "StatusEffectTheDarkMap"))
                {
                    if (HasComp<ShadekinComponent>(uid) || HasComp<TheDarkImmuneComponent>(uid))
                        remove = true;

                    if (!remove)
                        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
                            if (TryComp<TheDarkImmuneComponent>(entity, out var blocker) && blocker.Ranged)
                                remove = true;
                }

                if (AreWeInTheDark(uid) && !remove)
                    _status.TrySetStatusEffectDuration(uid, "StatusEffectTheDarkMap");
                else
                    _status.TryRemoveStatusEffect(uid, "StatusEffectTheDarkMap");
            }
        }
    }
}
