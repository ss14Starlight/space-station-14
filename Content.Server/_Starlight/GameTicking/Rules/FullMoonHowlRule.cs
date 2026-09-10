using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Server.Chat.Managers;
using Content.Server.StationEvents.Events;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Pinpointer;
using Content.Shared.Station.Components;
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

        // Prefer initial "main station" grid if available, otherwise fallback to all station grids.
        var grids = stationData.MainGrids.Count > 0
            ? stationData.MainGrids
            : stationData.Grids;

        // If no suitable grid is found, abort the event.
        if (!grids.TryFirstOrNull(out var mainGrid))
            return;

        // Recipients are selected by map, not grid, so off-grid players on the station map may still hear the announcement.
        var mainStationMap = Transform(mainGrid.Value).MapID;

        var recipients = Filter.Empty().AddWhere(session =>
        {
            if (session.AttachedEntity is null)
                return false;

            var attached = session.AttachedEntity.Value;

            if (Transform(attached).MapID != mainStationMap)
                return false;

            // Ghosts are always eligible to hear the announcement, for observer/admin purposes.
            if (HasComp<GhostComponent>(attached))
                return true;

            if (TryComp<InventoryComponent>(attached, out var inv)
                && inv.SpeciesId is { } speciesId
                && component.EligibleInventorySpecies.Contains(speciesId))
                return true;

            return false;
        });

        // Location candidates are restricted to beacon entities on main station grids only.
        var beaconLocations = new List<string>();
        var beaconQuery = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (beaconQuery.MoveNext(out var ent, out var beacon, out var xform))
        {
            if (!beacon.Enabled)
                continue;

            if (xform.GridUid is not { } gridUid || !grids.Contains(gridUid))
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
        // Send as styled chat text to avoid default announcement header and keep the tone.
        var wrappedMessage = $"[font color=#ADD8E6][italic]{FormattedMessage.EscapeText(message)}[/italic][/font]";
        _chatManager.ChatMessageToManyFiltered(recipients, ChatChannel.Radio, message, wrappedMessage, default, false, true, Color.FromHex("#ADD8E6"));
        Audio.PlayGlobal(component.HowlSound, recipients, true);
    }
}
