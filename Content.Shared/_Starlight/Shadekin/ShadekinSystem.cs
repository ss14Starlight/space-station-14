using Content.Shared.Humanoid;
using Content.Shared.Alert;
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
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared._Starlight.Light;
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

namespace Content.Shared._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedBodySystem _bodySystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private NullSpacePhaseSystem _nullspace = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedEnsnareableSystem _ensnareable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;
    [Dependency] private SharedLanguageSystem _language = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedIlluminationSystem _illumination = default!;

    [Dependency] private EntityQuery<DarkLightComponent> _darkLightQuery = default!;
    [Dependency] private EntityQuery<ShadegenAffectedComponent> _shadegenAffected = default!;

    private static readonly ProtoId<TagPrototype> _theDarkTag = "TheDark";
    private static readonly ProtoId<TagPrototype> _coreTag = "ShadekinCore";
    private static readonly ProtoId<TagPrototype> _damagedCoreTag = "DamagedShadekinCore";
    private static readonly ProtoId<DamageTypePrototype> _heatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> _cellularType = "Cellular";
    private static readonly EntProtoId<GameRuleComponent> _theDarkMap = "TheDarkMap";
    private static readonly EntProtoId _theDarkMapStatus = "StatusEffectTheDarkMap";

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateCooldown = TimeSpan.FromSeconds(1f);

    private readonly List<LightContribution> _lightSources = new();
    private readonly HashSet<EntityUid> _theDarkMaps = new();

    private bool _hadTheDarkMaps;

    private IlluminationQuery _lightQuery;

    public override void Initialize()
    {
        base.Initialize();

        _lightQuery = new IlluminationQuery(Ignore: IsIgnoredLight);
    }

    private bool IsIgnoredLight(EntityUid light)
        => _darkLightQuery.HasComp(light) || _shadegenAffected.HasComp(light);

    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<ShadekinComponent> _, ref BeforeDamageChangedEvent args)
        => args.Damage.DamageDict["Asphyxiation"] = 0;

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<ShadekinComponent> ent, ref ComponentShutdown _)
    {
        if (_timing.ApplyingState)
            return;
        RemComp<BrighteyeComponent>(ent);
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
            // TODO STARLIGHT predict this properly, right now all callers are on server
            // (PopupPredicted would skip the shadekin itself, since the server assumes they predicted it)
            if (_net.IsServer)
                _popup.PopupEntity(Loc.GetString("shadekin-shunt"), ent.Owner, ent.Owner, PopupType.LargeCaution);

            _stunSystem.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(1), autoStand: false);
            ApplyCoreDamage(ent.Owner, 5);
        }
    }

    public void UpdateAlert(EntityUid uid, ShadekinComponent component, short state)
        => _alerts.ShowAlert(uid, component.ShadekinAlert, state);

    /// <summary>
    /// Return an illumination float value with is how many "energy" of light is hitting our ent.
    /// Server only, the client has no light index and always gets 0.
    /// </summary>
    /// <remarks>
    /// Keeps the shadekin falloff (energy * (1 - (d/r)^2)^2) instead of the shader one,
    /// our thresholds are tuned for it. Lights come from <see cref="SharedIlluminationSystem"/>.
    /// </remarks>
    public float GetLightExposure(EntityUid uid, float cap = float.MaxValue)
    {
        var illumination = 0f;

        var targetCoords = _transform.GetMapCoordinates(uid);
        if (targetCoords.MapId == MapId.Nullspace)
            return illumination;

        // Shadegens make everything around them dark. There are only ever a few of them,
        // so check them directly instead of doing a spatial lookup.
        var shadeQuery = EntityQueryEnumerator<ShadegenComponent, TransformComponent>();
        while (shadeQuery.MoveNext(out _, out var shadegen, out var shadeXform))
        {
            if (shadeXform.MapID != targetCoords.MapId)
                continue;

            if ((_transform.GetWorldPosition(shadeXform) - targetCoords.Position).LengthSquared() <= shadegen.Range * shadegen.Range)
                return illumination;
        }

        _lightSources.Clear();
        _illumination.GetLightSources(uid, _lightSources, _lightQuery);

        foreach (var source in _lightSources)
        {
            if (source.Radius < 1f)
                continue;

            var denom = source.Distance / source.Radius;
            var attenuation = 1 - (denom * denom);
            illumination += source.Light.Comp1.Energy * attenuation * attenuation * source.Mask;

            if (illumination >= cap)
                break;
        }

        return illumination;
    }

    private void SetPassiveBuff(EntityUid uid, ShadekinState shadekinState)
    {
        if (!TryComp<PassiveDamageComponent>(uid, out var passive))
            return;

        switch (shadekinState)
        {
            case ShadekinState.Annoying or
                ShadekinState.High or
                ShadekinState.Extreme:
                break;
            case ShadekinState.Low:
                passive.AllowedStates.Clear();
                passive.AllowedStates.Add(MobState.Alive);
                passive.Interval = 1f;
                break;
            case ShadekinState.Dark:
                passive.AllowedStates.Clear();
                passive.AllowedStates.Add(MobState.Alive);
                passive.AllowedStates.Add(MobState.Critical);
                passive.AllowedStates.Add(MobState.Dead);
                passive.Interval = 0.5f;
                break;
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

    /// <summary>
    /// Light exposure above which the shadekin state can't get any worse.
    /// </summary>
    private static float GetMaxThreshold(ShadekinComponent component)
    {
        var max = float.MaxValue;
        // Sorted ascending, the last key is the highest.
        foreach (var threshold in component.Thresholds.Keys)
        {
            max = threshold.Float();
        }

        return max;
    }

    /// <returns>True if the state changed.</returns>
    private bool CheckThresholds(EntityUid uid, ShadekinComponent component, float lightExposure)
    {
        // The highest reached threshold decides the state. Being below the Low threshold means we're in the Dark.
        ShadekinState? selectedState = null;
        foreach (var (threshold, shadekinState) in component.Thresholds)
        {
            if (lightExposure < threshold)
            {
                if (shadekinState == ShadekinState.Low)
                    selectedState = ShadekinState.Dark;
            }
            else
                selectedState = shadekinState;
        }

        if (selectedState is not { } newState)
            return false;

        // Cheap when nothing changed, and brings the alert back if something cleared it.
        UpdateAlert(uid, component, (short) newState);

        if (component.CurrentState == newState)
            return false;

        component.CurrentState = newState;
        Dirty(uid, component);
        return true;
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

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<ShadekinComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (curTime < component.NextUpdate)
                continue;

            // First update gets a random offset, so shadekin spawned on the same tick (round start) don't all do
            // their light lookups and raycasts on the same tick every second.
            if (component.NextUpdate == TimeSpan.Zero)
            {
                component.NextUpdate = curTime + (component.UpdateCooldown * _random.NextDouble());
                continue;
            }

            component.NextUpdate = curTime + component.UpdateCooldown;

            var lightExposure = 0f;

            if (!HasComp<NullSpaceComponent>(uid) && !AreWeInTheDark(uid))
                lightExposure = GetLightExposure(uid, GetMaxThreshold(component));

            var stateChanged = CheckThresholds(uid, component, lightExposure);

            ToggleNightVision(uid, component.CurrentState);
            SetPassiveBuff(uid, component.CurrentState);

            // Our speed modifier only depends on the state, and other refreshes include it anyway.
            if (stateChanged)
                _speed.RefreshMovementSpeedModifiers(uid);

            if (component.CurrentState == ShadekinState.Extreme)
                ApplyLightDamage(uid, 1);

            if (TryComp<BrighteyeComponent>(uid, out var brighteye))
                UpdateEnergy(uid, component, brighteye);
        }

        // The Dark Effects - This only applies for Ents that are IN THE DARK.
        if (curTime > _nextUpdate)
        {
            _nextUpdate = curTime + _updateCooldown;
            UpdateTheDarkStatus();
        }
    }

    /// <summary>
    /// Gives "The Dark" status effect to mobs on The Dark map, and removes it from everyone else.
    /// </summary>
    private void UpdateTheDarkStatus()
    {
        _theDarkMaps.Clear();
        var mapQuery = EntityQueryEnumerator<MapComponent>();
        while (mapQuery.MoveNext(out var mapUid, out _))
        {
            if (_tag.HasTag(mapUid, _theDarkTag))
                _theDarkMaps.Add(mapUid);
        }

        // Nobody can be in the Dark or still have its status, skip going over every mob.
        var hadTheDarkMaps = _hadTheDarkMaps;
        _hadTheDarkMaps = _theDarkMaps.Count > 0;
        if (!_hadTheDarkMaps && !hadTheDarkMaps)
            return;

        var mobQuery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var uid, out _, out var xform))
        {
            var inTheDark = xform.MapUid is { } mapUid
                && _theDarkMaps.Contains(mapUid)
                && !IsImmuneToTheDark(uid, xform);

            var hasStatus = _status.HasStatusEffect(uid, _theDarkMapStatus);
            if (inTheDark && !hasStatus)
                _status.TrySetStatusEffectDuration(uid, _theDarkMapStatus);
            else if (!inTheDark && hasStatus)
                _status.TryRemoveStatusEffect(uid, _theDarkMapStatus);
        }
    }

    private bool IsImmuneToTheDark(EntityUid uid, TransformComponent xform)
    {
        if (HasComp<ShadekinComponent>(uid) || HasComp<TheDarkImmuneComponent>(uid))
            return true;

        foreach (var entity in _lookup.GetEntitiesIntersecting(xform.Coordinates))
        {
            if (TryComp<TheDarkImmuneComponent>(entity, out var blocker) && blocker.Ranged)
                return true;
        }

        return false;
    }
}
