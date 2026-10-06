using System.Linq;
using Content.Server._Starlight.CosmicCult.Components;
using Content.Server._Starlight.CosmicCult.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Popups;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Content.Shared.Station.Components;
using Robust.Shared.Random;

namespace Content.Server._Starlight.CosmicCult;

public sealed partial class MalignRiftSpawnRule : StationEventSystem<MalignRiftSpawnRuleComponent>
{
    private const int CrewPerRift = 6;

    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private CosmicRiftSystem _malignRift = default!;
    [Dependency] private PopupSystem _popup = null!;
    [Dependency] private GhostSystem _ghost = null!;
    [Dependency] private IRobustRandom _rand = null!;

    protected override void Added(EntityUid uid, MalignRiftSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        AdminLogManager.Add(LogType.EventAnnounced, $"Event added / announced: {ToPrettyString(uid)}");
    }
    protected override void Started(EntityUid uid, MalignRiftSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!TryComp<StationDataComponent>(chosenStation.Value, out var stationData))
            return;

        var stationEntity = (chosenStation.Value, stationData);
        var grid = StationSystem.GetLargestGrid(stationEntity);

        if (grid is null)
            return;

        if (_ticker.IsGameRuleActive<CosmicCultRuleComponent>())
            _ticker.EndGameRule(uid); // Cosmic cult's active! Don't actually proceed to the contents of the gamerule!
        else
        {
            var totalCrew = _playerMan.Sessions.Count(session => session.Status == SessionStatus.InGame && HasComp<HumanoidAppearanceComponent>(session.AttachedEntity));

            var mobquery = EntityQueryEnumerator<MobStateComponent>();
            while (mobquery.MoveNext(out var ent, out var _))
                if (StationSystem.IsEntityOnStation(ent, chosenStation, stationData))
                    _popup.PopupEntity(Loc.GetString("cosmiccult-announce-tier2-progress"), ent, ent, PopupType.LargeCaution);

            _audio.PlayGlobal(comp.Tier2Sound, StationSystem.GetInStation(stationData), false, AudioParams.Default);

            for (var i = 0; i < Convert.ToInt16(totalCrew / CrewPerRift); i++) // spawn # malign rifts equal to 16.67% of the playercount
                _malignRift.SpawnRift(grid.Value, comp.MalignRift);

            var lights = EntityQueryEnumerator<PoweredLightComponent>();
            while (lights.MoveNext(out var light, out _))
            {
                if (!StationSystem.IsEntityOnStation(light, chosenStation, stationData)) continue;
                if (!_rand.Prob(0.50f))
                    continue;
                _ghost.DoGhostBooEvent(light);
            }
        }
    }
}
