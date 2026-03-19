using Content.Server.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using AlertLevelDimmedLightComponent = Content.Shared._Starlight.Light.AlertLevelDimmedLightComponent;

#region Starlight
using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Station.Components; // Starlight
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Server._Starlight.GameTicking.Rules.Components; // SL
using Content.Shared.GameTicking.Components; // SL
using Content.Server.GameTicking; // SL
using Content.Server.Chat.Systems; // SL
using Robust.Shared.Player; // SL
#endregion Starlight

namespace Content.Server.Light.EntitySystems;

/// <summary>
///     System for the PoweredLightComponents
/// </summary>
public sealed class PoweredLightSystem : SharedPoweredLightSystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!; // SL
    [Dependency] private readonly ChatSystem _chat = default!; // SL
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!; // SL
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoweredLightComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PoweredLightComponent, GhostBooEvent>(OnGhostBoo);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged); //Starlight
    }

    private void OnGhostBoo(EntityUid uid, PoweredLightComponent light, GhostBooEvent args)
    {
        if (light.IgnoreGhostsBoo)
            return;

        // check cooldown first to prevent abuse
        var time = GameTiming.CurTime;
        if (light.LastGhostBlink != null)
        {
            if (time <= light.LastGhostBlink + light.GhostBlinkingCooldown)
                return;
        }

        light.LastGhostBlink = time;

        ToggleBlinkingLight(uid, light, true);
        uid.SpawnTimer(light.GhostBlinkingTime, () =>
        {
            ToggleBlinkingLight(uid, light, false);
        });

        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, PoweredLightComponent light, MapInitEvent args)
    {
        // TODO: Use ContainerFill dog
        if (light.HasLampOnSpawn != null)
        {
            var entity = EntityManager.SpawnEntity(light.HasLampOnSpawn, EntityManager.GetComponent<TransformComponent>(uid).Coordinates);
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
            UpdateLight(uid, light);
        }
    }
    #endregion Starlight
}
