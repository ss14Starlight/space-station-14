using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.AlertLevel;
using Content.Server.GameTicking;
using Content.Server.StationEvents.Components;
using Content.Shared._Starlight.GameTicking.Components;
using Content.Shared._Starlight.Light;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Station.Components;
using Robust.Shared.Player;

namespace Content.Server.StationEvents.Events;

public sealed class NightShiftRule : StationEventSystem<NightShiftRuleComponent>
{
    [Dependency] private readonly SharedPoweredLightSystem _poweredLightSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightShiftDimmedLightComponent, GetDimmedLightLevelEvent>(OnGetDimmedLightLevel);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }
    
    /// <summary>
    /// Enables night shift dimming on a station. The return value indicates if something happened or not.
    /// </summary>
    private bool EnableNightShiftDimming(EntityUid station, NightShiftRuleComponent nightShift)
    {
        // Find eligible powered lights.
        var query = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        var success = false;
        while (query.MoveNext(out var ent, out var light, out var xform))
        {
            // Ignore lights not on our station.
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;
            
            // Add our dimmer component.
            var dimmer = EnsureComp<NightShiftDimmedLightComponent>(ent);
            dimmer.LightEnergyMultiplier = nightShift.LightEnergyMultiplier;
            _poweredLightSystem.UpdateLight(ent, light);
            success = true;
        }

        return success;
    }

    /// <summary>
    /// Disables night shift dimming on a station. The return value indicates if something happened or not.
    /// </summary>
    private bool DisableNightShiftDimming(EntityUid station)
    {
        // Find all dimmed lights.
        var affectedLightQuery = EntityQueryEnumerator<PoweredLightComponent, NightShiftDimmedLightComponent, TransformComponent>();
        var success = false;
        while (affectedLightQuery.MoveNext(out var uid, out var poweredLight, out _, out var xform))
        {
            // Ignore lights not on our station.
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;
                
            // Remove our dimming component from currently dimmed lights.
            RemComp<NightShiftDimmedLightComponent>(uid);
            _poweredLightSystem.UpdateLight(uid, poweredLight);
            success = true;
        }

        return success;
    }
    
    /// <summary>
    /// React to alert level changes. Only used for disabling night shift dimming prematurely.
    /// </summary>
    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        var nightShiftQuery = EntityQueryEnumerator<NightShiftRuleComponent, GameRuleComponent>();
        while (nightShiftQuery.MoveNext(out var shift, out var nightShift, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleActive(shift, gameRule)) continue;
            if (!TryComp<StationEventComponent>(shift, out var stationEvent)) continue;
            if (stationEvent.TargetStation != ev.Station) continue;
            
            if (nightShift.PermittedAlertLevels.Contains(ev.AlertLevel))
            {
                // If the alert level is permitted, enable night shift dimming, and announce if that changed anything.
                if (EnableNightShiftDimming(ev.Station, nightShift))
                    Announce(stationEvent, Loc.GetString(nightShift.EnableAnnouncement), true);
            }
            else
            {
                // If the alert level is not permitted, disable night shift dimming, and announce if that changed anything.
                if (DisableNightShiftDimming(ev.Station))
                    Announce(stationEvent, Loc.GetString(nightShift.DisableAnnouncement), true);
            }
        }
    }
    
    private void OnGetDimmedLightLevel(EntityUid uid, NightShiftDimmedLightComponent component, GetDimmedLightLevelEvent args)
    {
        args.LightEnergy *= component.LightEnergyMultiplier;
        args.PowerUse *= component.LightEnergyMultiplier;
    }

    protected override void Started(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        // Determine what station to target.
        EntityUid? chosenStation;
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;
        chosenStation = stationEvent.TargetStation;
        if (chosenStation is null)
            if (!TryGetRandomStation(out chosenStation))
                return;
        
        EnableNightShiftDimming(chosenStation.Value, comp);
    }

    protected override void Ended(EntityUid uid, NightShiftRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, comp, gameRule, args);
        if (!TryComp<StationEventComponent>(uid, out var stationEvent)) return;

        DisableNightShiftDimming(stationEvent.TargetStation!.Value);
    }
}
