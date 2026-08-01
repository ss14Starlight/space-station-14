using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Server.Chat.Managers;
using Content.Server.StationEvents.Events;
using Content.Shared.Chat;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Pinpointer;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class FullMoonHowlRule : StationEventSystem<FullMoonHowlRuleComponent>
{
    [Dependency] private IChatManager _chatManager = default!;

    protected override void Started(EntityUid uid, FullMoonHowlRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        var chosenStation = stationEvent.TargetStation;
        if (chosenStation is null && !TryGetRandomStation(out chosenStation))
            return;

        if (!TryComp<StationDataComponent>(chosenStation.Value, out var stationData))
            return;

        var mainGrids = stationData.MainGrids;

        EntityUid? mainGrid = null;
        foreach (var grid in mainGrids)
        {
            if (!Exists(grid))
                continue;

            mainGrid = grid;
            break;
        }

        if (mainGrid is null)
            return;

        // Recipients are scoped by map, not grid membership, so off-grid players on the station map still qualify.
        var mainStationMap = Transform(mainGrid.Value).MapID;

        var recipients = Filter.Empty().AddWhere(session =>
        {
            if (session.AttachedEntity is null)
                return false;

            var attached = session.AttachedEntity.Value;

            if (Transform(attached).MapID != mainStationMap)
                return false;

            if (!TryComp<HumanoidAppearanceComponent>(attached, out var humanoid))
                return false;

            return humanoid.Species == "Vulpkanin" || humanoid.Species == "ProtoVulp";
        });

        // Location candidates are restricted to beacon entities on main station grids only.
        var beaconLocations = new List<string>();
        var beaconQuery = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (beaconQuery.MoveNext(out var ent, out var beacon, out var xform))
        {
            if (!beacon.Enabled)
                continue;

            if (xform.GridUid is not { } gridUid || !mainGrids.Contains(gridUid))
                continue;

            var locationName = !string.IsNullOrWhiteSpace(beacon.Text)
                ? beacon.Text
                : MetaData(ent).EntityName;

            if (!string.IsNullOrWhiteSpace(locationName))
                beaconLocations.Add(locationName);
        }

        var location = beaconLocations.Count > 0
            ? RobustRandom.Pick(beaconLocations)
            : Loc.GetString("station-event-fullmoonhowl-default-location");

        var message = Loc.GetString("station-event-fullmoonhowl-announcement", ("location", (object) location));
        // Send as styled chat text to avoid default announcement header and keep the dreamlike tone.
        var wrappedMessage = $"[font color=#ADD8E6][italic]{FormattedMessage.EscapeText(message)}[/italic][/font]";
        _chatManager.ChatMessageToManyFiltered(recipients, ChatChannel.Radio, message, wrappedMessage, default, false, true, Color.FromHex("#ADD8E6"));
        Audio.PlayGlobal(component.HowlSound, recipients, true);
    }
}
