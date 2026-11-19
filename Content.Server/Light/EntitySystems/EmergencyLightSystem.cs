using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.Light.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Examine;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Color = Robust.Shared.Maths.Color;

namespace Content.Server.Light.EntitySystems;

public sealed partial class EmergencyLightSystem : SharedEmergencyLightSystem
{
    [Dependency] private AmbientSoundSystem _ambient = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmergencyLightComponent, EmergencyLightEvent>(OnEmergencyLightEvent);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<EmergencyLightComponent, ExaminedEvent>(OnEmergencyExamine);
        SubscribeLocalEvent<EmergencyLightComponent, PowerChangedEvent>(OnEmergencyPower);
    }

    private void OnEmergencyPower(Entity<EmergencyLightComponent> entity, ref PowerChangedEvent args)
    {
        var meta = MetaData(entity.Owner);

        // TODO: PowerChangedEvent shouldn't be issued for paused ents but this is the world we live in.
        if (meta.EntityLifeStage >= EntityLifeStage.Terminating ||
            meta.EntityPaused)
        {
            return;
        }

        UpdateState(entity);
    }

    private void OnEmergencyExamine(EntityUid uid, EmergencyLightComponent component, ExaminedEvent args)
    {
        using (args.PushGroup(nameof(EmergencyLightComponent)))
        {
            args.PushMarkup(
                Loc.GetString("emergency-light-component-on-examine",
                    ("batteryStateText",
                        Loc.GetString(component.BatteryStateText[component.State]))));

            // Show alert level on the light itself.
            if (!TryComp<AlertLevelComponent>(_station.GetOwningStation(uid), out var alerts))
                return;

            if (alerts.AlertLevels == null)
                return;

            var name = alerts.CurrentLevel;

            var color = Color.White;
            if (alerts.AlertLevels.Levels.TryGetValue(alerts.CurrentLevel, out var details))
                color = details.Color;

            args.PushMarkup(
                Loc.GetString("emergency-light-component-on-examine-alert",
                    ("color", color.ToHex()),
                    ("level", Loc.GetString($"alert-level-{name.ToString().ToLower()}"))));
        }
    }

    private void OnEmergencyLightEvent(EntityUid uid, EmergencyLightComponent component, EmergencyLightEvent args)
    {
        switch (args.State)
        {
            case EmergencyLightState.On:
            case EmergencyLightState.Charging:
                EnsureComp<ActiveEmergencyLightComponent>(uid);
                break;
            case EmergencyLightState.Full:
            case EmergencyLightState.Empty:
                RemComp<ActiveEmergencyLightComponent>(uid);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // Starlight-start: simplified alert level change handler
    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        if (alert.AlertLevels == null || !alert.AlertLevels.Levels.TryGetValue(ev.AlertLevel, out _))
            return;

        var query = EntityQueryEnumerator<EmergencyLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != ev.Station)
                continue;

            UpdateState((uid, light));
        }
    }
    // Starlight-end

    public void SetState(EntityUid uid, EmergencyLightComponent component, EmergencyLightState state)
    {
        if (component.State == state) return;

        component.State = state;
        RaiseLocalEvent(uid, new EmergencyLightEvent(state));
    }

    //Starlight: -5 seconds from each integration test with a large map.
    protected override void AccUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<ActiveEmergencyLightComponent, EmergencyLightComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out _, out var emergencyLight, out var battery))
        {
            Update((uid, emergencyLight), battery, frameTime);
        }
    }

    private void Update(Entity<EmergencyLightComponent> entity, BatteryComponent battery, float frameTime)
    {
        if (entity.Comp.State == EmergencyLightState.On)
        {
            // Starlight-start: Do not drain battery if station power is active
            if (TryComp<ApcPowerReceiverComponent>(entity.Owner, out var receiver) && receiver.Powered)
                return;
            // Starlight-end

            if (!_battery.TryUseCharge((entity.Owner, battery), entity.Comp.Wattage * frameTime))
            {
                SetState(entity.Owner, entity.Comp, EmergencyLightState.Empty);
                TurnOff(entity);
            }
        }
        else
        {
            _battery.ChangeCharge((entity.Owner, battery), entity.Comp.ChargingWattage * frameTime * entity.Comp.ChargingEfficiency);
            if (_battery.IsFull((entity.Owner, battery)))
            {
                if (TryComp<ApcPowerReceiverComponent>(entity.Owner, out var receiver))
                {
                    receiver.Load = 1;
                }

                SetState(entity.Owner, entity.Comp, EmergencyLightState.Full);
            }
        }
    }

    /// <summary>
    ///     Updates the light's power drain, battery drain, sprite and actual light state.
    /// </summary>
    public void UpdateState(Entity<EmergencyLightComponent> entity)
    {
        if (!TryComp<ApcPowerReceiverComponent>(entity.Owner, out var receiver))
            return;

        // Starlight-start: correct alert light color, force enable, and APC load resolution
        var color = Color.Red;
        var forceEnable = false;

        if (TryComp<AlertLevelComponent>(_station.GetOwningStation(entity.Owner), out var alerts)
            && alerts.AlertLevels != null
            && alerts.AlertLevels.Levels.TryGetValue(alerts.CurrentLevel, out var details))
        {
            color = details.EmergencyLightColor;
            forceEnable = details.ForceEnableEmergencyLights;
        }

        entity.Comp.ForciblyEnabled = forceEnable;

        if (entity.Comp.ForciblyEnabled)
        {
            if (receiver.Powered)
                receiver.Load = (int) Math.Abs(entity.Comp.Wattage);

            TurnOn(entity, color);
            SetState(entity.Owner, entity.Comp, EmergencyLightState.On);
        }
        else if (!receiver.Powered)
        {
            TurnOn(entity, Color.Red);
            SetState(entity.Owner, entity.Comp, EmergencyLightState.On);
        }
        else // Powered on normal level (e.g. green alert)
        {
            receiver.Load = (int) Math.Abs(entity.Comp.Wattage);
            TurnOff(entity, color);
            SetState(entity.Owner, entity.Comp, EmergencyLightState.Charging);
        }
        // Starlight-end
    }

    private void TurnOff(Entity<EmergencyLightComponent> entity)
    {
        _pointLight.SetEnabled(entity.Owner, false);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.On, false);
        _ambient.SetAmbience(entity.Owner, false);
    }

    /// <summary>
    ///     Turn off emergency light and set color.
    /// </summary>
    private void TurnOff(Entity<EmergencyLightComponent> entity, Color color)
    {
        _pointLight.SetEnabled(entity.Owner, false);
        _pointLight.SetColor(entity.Owner, color);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.Color, color);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.On, false);
        _ambient.SetAmbience(entity.Owner, false);
    }

    private void TurnOn(Entity<EmergencyLightComponent> entity)
    {
        _pointLight.SetEnabled(entity.Owner, true);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.On, true);
        _ambient.SetAmbience(entity.Owner, true);
    }

    /// <summary>
    ///     Turn on emergency light and set color.
    /// </summary>
    private void TurnOn(Entity<EmergencyLightComponent> entity, Color color)
    {
        _pointLight.SetEnabled(entity.Owner, true);
        _pointLight.SetColor(entity.Owner, color);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.Color, color);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.On, true);
        _ambient.SetAmbience(entity.Owner, true);
    }
}
