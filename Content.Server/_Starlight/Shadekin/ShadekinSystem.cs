using Content.Shared.Humanoid;
using Content.Shared.Alert;
using System.Linq;
using Robust.Server.GameObjects;
using Content.Shared.Examine;
using Robust.Server.Containers;
using Content.Shared._Starlight.Shadekin;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Damage;
using Robust.Shared.Timing;
using Content.Shared._Starlight.NullSpace;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Station;
using Content.Shared.Popups;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Random;
using Content.Server._Starlight.NullSpace;
using Content.Server._Starlight.Bluespace;
using Content.Server.Stunnable;
using Content.Shared.Damage.Systems;
using Content.Server.DoAfter;
using Content.Shared.Ensnaring;
using Robust.Shared.Audio.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map.Components;
using Content.Server.GameTicking;
using Content.Shared._Starlight.Medical.Body.Events;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
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
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedEnsnareableSystem _ensnareable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private static readonly ProtoId<TagPrototype> _theDarkTag = "TheDark";
    private static readonly ProtoId<TagPrototype> _coreTag = "ShadekinCore";
    private static readonly ProtoId<TagPrototype> _damagedCoreTag = "DamagedShadekinCore";
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private TimeSpan _updateCooldown = TimeSpan.FromSeconds(1f);

    private sealed class LightCone
    {
        public float Direction { get; set; }
        public float InnerWidth { get; set; }
        public float OuterWidth { get; set; }
    }
    private readonly Dictionary<string, List<LightCone>> lightMasks = new()
    {
        ["/Textures/Effects/LightMasks/cone.png"] = new List<LightCone>
    {
        new LightCone { Direction = 0, InnerWidth = 30, OuterWidth = 60 }
    },
        ["/Textures/Effects/LightMasks/double_cone.png"] = new List<LightCone>
    {
        new LightCone { Direction = 0, InnerWidth = 30, OuterWidth = 60 },
        new LightCone { Direction = 180, InnerWidth = 30, OuterWidth = 60 }
    }
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganShadekinCoreComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<OrganShadekinCoreComponent, OrganAddedToBodyEvent>(CoreOrganInit);

        SubscribeLocalEvent<ShadekinComponent, ComponentShutdown>((uid, _, _) => RemComp<BrighteyeComponent>(uid));
        SubscribeLocalEvent<ShadekinComponent, EyeColorInitEvent>(OnEyeColorChange);
        SubscribeLocalEvent<ShadekinComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<ShadekinComponent, NullSpaceShuntEvent>(NullSpaceShunt);
        SubscribeLocalEvent<ShadekinComponent, BeforeDamageChangedEvent>((uid, _, args) => args.Damage.DamageDict["Asphyxiation"] = 0);

        InitializeBrighteye();
        InitializeAbilities();
    }

    private void CoreOrganInit(EntityUid uid, OrganShadekinCoreComponent component, OrganAddedToBodyEvent args)
    {
        if (component.OrganOwner is null)
            component.OrganOwner = args.Body;
    }

    private void OnExamined(EntityUid uid, OrganShadekinCoreComponent component, ref ExaminedEvent args)
    {
        if (!component.Damaged)
            args.PushMarkup(Loc.GetString("shadekin-core-undamaged"));

        if (component.OrganOwner == args.Examiner)
            args.PushMarkup(Loc.GetString("shadekin-core-owner"));
    }

    private void OnEyeColorChange(EntityUid uid, ShadekinComponent component, EyeColorInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        humanoid.EyeGlowing = false;
        Dirty(uid, humanoid);
    }

    private void NullSpaceShunt(EntityUid uid, ShadekinComponent component, NullSpaceShuntEvent args)
    {
        if (TryComp<BodyComponent>(uid, out var body) && _bodySystem.TryGetOrgansWithComponent<OrganShadekinCoreComponent>((uid, body), out _)) // Wizden
        {
            _popup.PopupEntity(Loc.GetString("shadekin-shunt"), uid, uid, PopupType.LargeCaution);
            _stunSystem.TryKnockdown(uid, TimeSpan.FromSeconds(1), autoStand: false);
            ApplyCoreDamage(uid, 5);
        }
    }

    public void UpdateAlert(EntityUid uid, ShadekinComponent component, short state)
    {
        _alerts.ShowAlert(uid, component.ShadekinAlert, state);
    }

    private Angle GetAngle(EntityUid lightUid, SharedPointLightComponent lightComp, EntityUid targetUid)
    {
        var (lightPos, lightRot) = _transform.GetWorldPositionRotation(lightUid);
        lightPos += lightRot.RotateVec(lightComp.Offset);

        var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetUid);

        var mapDiff = targetPos - lightPos;

        var oppositeMapDiff = (-lightRot).RotateVec(mapDiff);
        var angle = oppositeMapDiff.ToWorldAngle();

        if (angle == double.NaN && _transform.ContainsEntity(targetUid, lightUid) || _transform.ContainsEntity(lightUid, targetUid))
        {
            angle = 0f;
        }

        return angle;
    }

    /// <summary>
    /// Return an illumination float value with is how many "energy" of light is hitting our ent.
    /// WARNING: This function might be expensive, Avoid calling it too much and CACHE THE RESULT!
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public float GetLightExposure(EntityUid uid)
    {
        var illumination = 0f;

        var shadeQuery = _lookup.GetEntitiesInRange<ShadegenComponent>(Transform(uid).Coordinates, 10); // Why 10 when theres different ranges? because light check does not go above 20.

        foreach (var shadegen in shadeQuery)
            if (_transform.InRange(Transform(uid).Coordinates, Transform(shadegen.Owner).Coordinates, shadegen.Comp.Range))
                return illumination;

        var lightQuery = _lookup.GetEntitiesInRange<PointLightComponent>(Transform(uid).Coordinates, 10, LookupFlags.All | LookupFlags.Approximate);

        foreach (var light in lightQuery)
        {
            if (HasComp<DarkLightComponent>(light.Owner) || HasComp<ShadegenAffectedComponent>(light.Owner))
                continue;

            if (!light.Comp.Enabled
                || light.Comp.Radius < 1
                || light.Comp.Energy <= 0)
                continue;

            // Check if our entity is in a container with OccludesLight, if yes, is it the same as the light?
            if (_container.TryGetContainingContainer(uid, out var uidcontainer) && uidcontainer.OccludesLight && !_container.IsInSameOrNoContainer(uid, light.Owner))
                continue;

            // Same as above but this time we check the light entity instead of our entity.
            if (_container.TryGetContainingContainer(light.Owner, out var lightcontainer) && lightcontainer.OccludesLight && !_container.IsInSameOrNoContainer(uid, light.Owner))
                continue;

            if (!_examine.InRangeUnOccluded(light, uid, light.Comp.Radius, null))
                continue;

            Transform(uid).Coordinates.TryDistance(EntityManager, Transform(light).Coordinates, out var dist);

            var denom = dist / light.Comp.Radius;
            var attenuation = 1 - (denom * denom);
            var calculatedLight = 0f;

            if (light.Comp.MaskPath is not null)
            {
                var angleToTarget = GetAngle(light, light.Comp, uid);
                foreach (var cone in lightMasks[light.Comp.MaskPath])
                {
                    var coneLight = 0f;
                    var angleAttenuation = (float)Math.Min((float)Math.Max(cone.OuterWidth - angleToTarget, 0f), cone.InnerWidth) / cone.OuterWidth;

                    if (angleToTarget.Degrees - cone.Direction > cone.OuterWidth)
                        continue;
                    else if (angleToTarget.Degrees - cone.Direction > cone.InnerWidth
                        && angleToTarget.Degrees - cone.Direction < cone.OuterWidth)
                        coneLight = light.Comp.Energy * attenuation * attenuation * angleAttenuation;
                    else
                        coneLight = light.Comp.Energy * attenuation * attenuation;

                    calculatedLight = Math.Max(calculatedLight, coneLight);
                }
            }
            else
                calculatedLight = light.Comp.Energy * attenuation * attenuation;

            illumination += calculatedLight; //Math.Max(illumination, calculatedLight);
        }

        return illumination;
    }

    private void SetPassiveBuff(EntityUid uid, ShadekinState shadekinState)
    {
        if (!TryComp<PassiveDamageComponent>(uid, out var passive))
            return;

        if (shadekinState == ShadekinState.Annoying ||
            shadekinState == ShadekinState.High ||
            shadekinState == ShadekinState.Extreme)
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
        damage.DamageDict.Add("Heat", dmg);
        _damageable.TryChangeDamage(uid, damage, true, false);
    }

    private void ApplyCoreDamage(EntityUid uid, float dmg)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Cellular", dmg);
        _damageable.TryChangeDamage(uid, damage, false, false);
    }

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, ShadekinComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.CurrentState == ShadekinState.High || component.CurrentState == ShadekinState.Extreme)
        {
            if (!TryComp<MovementSpeedModifierComponent>(uid, out var movement))
                return;

            var sprintDif = movement.BaseWalkSpeed / movement.BaseSprintSpeed;
            args.ModifySpeed(1f, sprintDif);
        }
    }

    private void ToggleNightVision(EntityUid uid, ShadekinState shadekinState)
    {
        if (shadekinState == ShadekinState.Dark)
            EnsureComp<NightVisionComponent>(uid);
        else if (TryComp<NightVisionComponent>(uid, out var nightvision) && !nightvision.Clothes)
            RemComp<NightVisionComponent>(uid);
    }

    private void CheckThresholds(EntityUid uid, ShadekinComponent component, float lightExposure)
    {
        foreach (var (threshold, shadekinState) in component.Thresholds.Reverse())
        {
            var selectedstate = shadekinState;
            if (lightExposure < threshold)
            {
                if (selectedstate == ShadekinState.Low) // If Low is below the threshold, then we auto-jump to Dark.
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

    /// <summary>
    /// Makes a simple check to see if the ent is in the dark.
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public bool AreWeInTheDark(EntityUid uid)
    {
        var mapUid = Transform(uid).MapUid;
        if (mapUid is not null && _tag.HasTag(mapUid.Value, _theDarkTag))
            return true;

        return false;
    }

    /// <summary>
    /// Spawn "The Dark"
    /// </summary>
    public void SpawnTheDark()
    {
        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var mapuid, out var mapcomp))
        {
            if (mapcomp.MapPaused)
                continue;

            if (_tag.HasTag(mapuid, _theDarkTag))
                return;
        }
        _gameTicker.StartGameRule("TheDarkMap");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShadekinComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextUpdate)
                continue;

            component.NextUpdate = _timing.CurTime + component.UpdateCooldown;

            var lightExposure = 0f;

            if (HasComp<NullSpaceComponent>(uid) || AreWeInTheDark(uid)) // Were in NullSpace, NullSpace is dark... and "The Dark" is dark too!
            {
                // I had a brain moment, apprently if one is false its does not check for the other?
            }
            else
                lightExposure = GetLightExposure(uid);

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
