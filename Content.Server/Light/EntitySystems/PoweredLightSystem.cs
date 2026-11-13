using Content.Shared.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared._Starlight.Light;
using Content.Server.AlertLevel;
using Content.Shared.Station.Components;

namespace Content.Server.Light.EntitySystems;

/// <summary>
///     System for the PoweredLightComponents
/// </summary>
public sealed partial class PoweredLightSystem : SharedPoweredLightSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoweredLightComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PoweredLightComponent, GhostBooEvent>(OnGhostBoo);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged); //Starlight
    }

    private void OnGhostBoo(EntityUid uid, PoweredLightComponent light, GhostBooEvent args)
    {
        if (light.IgnoreGhostsBoo || HasComp<BlinkingPoweredLightComponent>(uid))
            return; // The light is immune or already blinking.

        // check cooldown first to prevent abuse
        var curTime = GameTiming.CurTime;
        if (light.LastGhostBlink != null && curTime <= light.LastGhostBlink + light.GhostBlinkingCooldown)
            return;

        light.LastGhostBlink = curTime;

        var blinkingComp = EnsureComp<BlinkingPoweredLightComponent>(uid);
        blinkingComp.StopBlinkingTime = curTime + light.GhostBlinkingTime;
        Dirty(uid, blinkingComp);

        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, PoweredLightComponent light, MapInitEvent args)
    {
        // TODO: Use ContainerFill dog
        if (light.HasLampOnSpawn != null)
        {
            var entity = Spawn(light.HasLampOnSpawn, Comp<TransformComponent>(uid).Coordinates);
            ContainerSystem.Insert(entity, light.LightBulbContainer);
        }
        // need this to update visualizers
        UpdateLight(uid, light);
    }

    #region Starlight
    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        if (!TryComp<AlertLevelComponent>(args.Station, out var alertLevelComp)) return;
        if (alertLevelComp.AlertLevels == null) return;
        if (!alertLevelComp.AlertLevels.Levels.TryGetValue(args.AlertLevel, out var levelAfter)) return;

        var query = EntityQueryEnumerator<PoweredLightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (!TryComp<StationMemberComponent>(Transform(uid).GridUid, out var stationMember)) continue;
            if (stationMember.Station != args.Station) continue;

            // If the new alert level requires no dimming, remove our dimming component.
            if (!levelAfter.DimmedLightMultiplier.HasValue)
            {
                RemComp<AlertLevelDimmedLightComponent>(uid);
                UpdateLight(uid, light);
                continue;
            }

            // Otherwise, ensure the component exists and set its value.
            var alertLevelDimming = EnsureComp<AlertLevelDimmedLightComponent>(uid);
            alertLevelDimming.LightEnergyMultiplier = levelAfter.DimmedLightMultiplier.Value;
            Dirty(uid, alertLevelDimming);
            UpdateLight(uid, light);
        }
    }
    #endregion Starlight
}
