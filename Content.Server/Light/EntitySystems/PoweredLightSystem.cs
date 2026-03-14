using Content.Server.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;

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
        var nightshiftannonced = false;
        var query = EntityQueryEnumerator<PoweredLightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (!TryComp<StationMemberComponent>(Transform(uid).GridUid, out var stationMember)) continue;
            if (stationMember.Station != args.Station) continue;

            var nightshiftquery = EntityQueryEnumerator<NightShiftRuleComponent, GameRuleComponent>();
            while (nightshiftquery.MoveNext(out var shift, out var _, out var gameRule))
                if (_gameTicker.IsGameRuleActive(shift, gameRule))
                {
                    var allPlayersOnStation = Filter.Empty().AddWhere(session =>
                    {
                        if (session.AttachedEntity is null) return false;
                        if (!TryComp<StationMemberComponent>(Transform(session.AttachedEntity.Value).GridUid,
                            out var stationGrid)) return false;
                        return stationGrid.Station == args.Station;
                    });

                    if (light.NightModeEnabled && !(args.AlertLevel == "green" || args.AlertLevel == "blue"))
                    {
                        if (!nightshiftannonced)
                        {
                            nightshiftannonced = true;
                            _chat.DispatchFilteredAnnouncement(allPlayersOnStation, Loc.GetString("station-event-nightshift-alert"));
                        }

                        SetNightMode(uid, false, light);
                    }
                    else if (!light.NightModeEnabled)
                    {
                        if (!nightshiftannonced)
                        {
                            nightshiftannonced = true;
                            _chat.DispatchFilteredAnnouncement(allPlayersOnStation, Loc.GetString("station-event-nightshift-calm"));
                        }

                        SetNightMode(uid, true, light);
                    }
                }

            if (args.AlertLevel == "delta" || args.AlertLevel == "epsilon" || args.AlertLevel == "omega" || args.AlertLevel == "theta")
                SetState(uid, false, light);
            else
                SetState(uid, true, light);
        }
    }
    #endregion Starlight
}
