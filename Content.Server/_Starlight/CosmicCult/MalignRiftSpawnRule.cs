using System.Linq;
using Content.Server._Starlight.CosmicCult.Components;
using Content.Server._Starlight.CosmicCult.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.CosmicCult;

public sealed partial class MalignRiftSpawnRule : StationEventSystem<MalignRiftSpawnRuleComponent>
{
    private const int CrewPerRift = 6;

    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private CosmicRiftSystem _malignRift = default!;

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

        if (chosenStation is null)
            return;

        if (!TryComp<StationDataComponent>(chosenStation.Value, out var stationData))
            return;

        var stationEntity = (chosenStation.Value, stationData);
        var grid = StationSystem.GetLargestGrid(stationEntity);

        if (grid is null)
            return;

        if (_ticker.IsGameRuleActive<CosmicCultRuleComponent>())
        {
            _ticker.EndGameRule(uid); // Cosmic cult's active! Don't actually proceed to the contents of the gamerule!
        }
        else
        {
            var totalCrew = _playerMan.Sessions.Count(session => session.Status == SessionStatus.InGame && HasComp<HumanoidAppearanceComponent>(session.AttachedEntity));
            var sender = Loc.GetString("cosmiccult-announcement-sender");

            _chatSystem.DispatchStationAnnouncement(chosenStation.Value, Loc.GetString("cosmiccult-announce-tier2-progress"), sender, false, null, Color.FromHex("#4cabb3"));
            _audio.PlayGlobal(comp.Tier2Sound, Filter.Broadcast(), false, AudioParams.Default);

            for (var i = 0; i < Convert.ToInt16(totalCrew / CrewPerRift); i++) // spawn # malign rifts equal to 16.67% of the playercount
            {
                _malignRift.SpawnRift(grid.Value, comp.MalignRift);
            }
        }
    }
}
