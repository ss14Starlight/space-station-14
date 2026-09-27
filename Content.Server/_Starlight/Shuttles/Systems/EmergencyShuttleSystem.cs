// ReSharper disable CheckNamespace

using System.Linq;
using Content.Server.Shuttles.Components;
using Content.Shared._Starlight.Commands;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    /// <summary>
    /// Sends shuttle dock announcement to all players on a station, removing all recipients from a filter.
    /// </summary>
    private void SendShuttleAnnouncement(LocId announcementText, Entity<StationEmergencyShuttleComponent> station,
        SoundSpecifier sound, Filter filter)
    {
        var allPlayersOnStation = Filter.Empty().AddWhere(session =>
        {
            if (session.AttachedEntity is null) return false;
            if (!TryComp<StationMemberComponent>(Transform(session.AttachedEntity.Value).GridUid,
                    out var stationGrid)) return false;
            return stationGrid.Station == station.Owner;
        });
        filter.RemoveWhere(x => allPlayersOnStation.Recipients.Contains(x));
        _chatSystem.DispatchFilteredAnnouncement(allPlayersOnStation, Loc.GetString(announcementText),
            announcementSound: sound);
    }

    /// <summary>
    /// Sends a separate shuttle dock announcement to all remaining players, so anyone in space, salvie planet, etc.
    /// </summary>
    private void SendShuttleAnnouncement(LocId announcementText, SoundSpecifier sound, Filter filter) =>
        _chatSystem.DispatchFilteredAnnouncement(filter, Loc.GetString(announcementText), announcementSound: sound);

    public void DelayShuttleDeparture(float seconds, ICommonSession? session = null)
    {
        if (ShuttlesLeft) return;

        _consoleAccumulator += seconds;
        _logger.Add(LogType.AdminCommands,
            $"{CommandHelpers.PlayerNameOrServer(session)} delayed the emergency shuttle launch by {seconds} seconds.");
    }

    public void DelayShuttleArrival(float seconds, ICommonSession? session = null)
    {
        if (EmergencyShuttleArrived) return;
        _roundEnd.DelayShuttleTimer(seconds);
        _logger.Add(LogType.AdminCommands,
            $"{CommandHelpers.PlayerNameOrServer(session)} delayed the emergency shuttle arrival by {seconds} seconds.");
    }
}
