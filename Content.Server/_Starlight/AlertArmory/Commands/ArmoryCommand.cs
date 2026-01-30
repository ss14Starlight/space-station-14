using System.Linq;
using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Starlight.AlertArmory.Commands;

/// <summary>
/// Call/Recall Armory shuttles.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class ArmoryCommand : IConsoleCommand
{
    public string Command => "armory";
    public string Description => "Send, recall, or list armory shuttles.";
    public string Help => GetDynamicHelp();
    private const string NoStationError = "No station with armories found.";
    private const string NoArmoryConfigError = "Station does not have armory shuttles configured.";
    private const string UnknownArmoryError = "Unknown armory '{0}'.";
    private const string InTransitError = "Armory '{0}' is currently in transit.";
    private const string SendingMessage = "Sending armory '{0}' to the station.";
    private const string SendFailError = "Failed to send armory '{0}'. Make sure it isn't already at the station.";
    private const string RecallingMessage = "Recalling armory '{0}' back to armory space.";
    private const string RecallFailError = "Failed to recall armory '{0}'. Make sure it isn't already in armory space.";

    private static string GetDynamicHelp()
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var availableArmories = new HashSet<string>();

        // Get armory keys
        var query = entMan.EntityQueryEnumerator<AlertArmoryStationComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            foreach (var key in comp.Grids.Keys)
                availableArmories.Add(key);
        }

        var armoryList = availableArmories.Count > 0
            ? string.Join(", ", availableArmories.OrderBy(k => k))
            : "No armories";

        return "Usage: armory <subcommand> <armory>\n" +
               "\n" +
               "Subcommands:\n" +
               "  list             Lists all available armories and their current status.\n" +
               "  send <armory>    Sends the specified armory to the station.\n" +
               "  recall <armory>  Recalls the specified armory back to armory space.";
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var stationSystem = entMan.System<StationSystem>();
        var armorySystem = entMan.System<AlertArmorySystem>();

        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        // Try to find the station
        var stationUid = GetStationUid(shell, entMan, stationSystem);

        if (stationUid == null)
        {
            shell.WriteError(NoStationError);
            return;
        }

        if (!entMan.TryGetComponent<AlertArmoryStationComponent>(stationUid.Value, out var stationComp))
        {
            shell.WriteError(NoArmoryConfigError);
            return;
        }

        var action = args[0].ToLowerInvariant();

        switch (action)
        {
            case "list":
                ListArmories(shell, entMan, stationUid.Value, stationComp);
                break;

            case "send":
                HandleSend(shell, entMan, armorySystem, stationUid.Value, stationComp, args);
                break;

            case "recall":
                HandleRecall(shell, entMan, armorySystem, stationUid.Value, stationComp, args);
                break;

            default:
                shell.WriteError($"Unknown subcommand '{action}'. Use 'list', 'send', or 'recall'.");
                break;
        }
    }

    private static EntityUid? GetStationUid(IConsoleShell shell, IEntityManager entMan, StationSystem stationSystem)
    {
        // Try to get station
        if (shell.Player?.AttachedEntity != null)
        {
            var playerStation = stationSystem.GetOwningStation(shell.Player.AttachedEntity.Value);
            if (playerStation != null)
                return playerStation;
        }

        // Fallback
        var query = entMan.EntityQueryEnumerator<AlertArmoryStationComponent>();
        while (query.MoveNext(out var uid, out _))
            return uid;

        return null;
    }

    private static void ListArmories(IConsoleShell shell, IEntityManager entMan, EntityUid stationUid, AlertArmoryStationComponent stationComp)
    {
        var stationName = entMan.GetComponent<MetaDataComponent>(stationUid).EntityName;
        shell.WriteLine($"Available armories for station {stationName}:");

        foreach (var (armoryKey, shuttle) in stationComp.Grids)
        {
            var shuttleComp = entMan.GetComponent<AlertArmoryShuttleComponent>(shuttle);
            var xform = entMan.GetComponent<TransformComponent>(shuttle);
            string location;
            if (shuttleComp.InTransit)
                location = "In Transit";
            else
                location = xform.MapUid == shuttleComp.ArmorySpaceUid ? "In Armory Space" : "At Station";

            shell.WriteLine($"  {armoryKey} - {location}");
        }
    }

    private static void HandleSend(IConsoleShell shell, IEntityManager entMan, AlertArmorySystem armorySystem, EntityUid stationUid, AlertArmoryStationComponent stationComp, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Usage: armory send <armory>");
            return;
        }

        if (!ValidateAndCheckTransit(shell, entMan, stationComp, args[1].ToLowerInvariant(), out var armoryKey))
            return;

        if (armorySystem.SendArmory(stationUid, armoryKey))
            shell.WriteLine(string.Format(SendingMessage, armoryKey));
        else
            shell.WriteError(string.Format(SendFailError, armoryKey));
    }

    private static void HandleRecall(IConsoleShell shell, IEntityManager entMan, AlertArmorySystem armorySystem, EntityUid stationUid, AlertArmoryStationComponent stationComp, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Usage: armory recall <armory>");
            return;
        }

        if (!ValidateAndCheckTransit(shell, entMan, stationComp, args[1].ToLowerInvariant(), out var armoryKey))
            return;

        if (armorySystem.RecallArmory(stationUid, armoryKey))
            shell.WriteLine(string.Format(RecallingMessage, armoryKey));
        else
            shell.WriteError(string.Format(RecallFailError, armoryKey));
    }

    /// Makes sure that the armory exists and is not in transit.
    private static bool ValidateAndCheckTransit(IConsoleShell shell, IEntityManager entMan, AlertArmoryStationComponent stationComp, string armoryKey, out string validatedKey)
    {
        validatedKey = armoryKey;

        if (!stationComp.Grids.TryGetValue(armoryKey, out var shuttle))
        {
            shell.WriteError(string.Format(UnknownArmoryError, armoryKey));
            return false;
        }

        var shuttleComp = entMan.GetComponent<AlertArmoryShuttleComponent>(shuttle);
        if (shuttleComp.InTransit)
        {
            shell.WriteError(string.Format(InTransitError, armoryKey));
            return false;
        }

        return true;
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();

        if (args.Length == 1)
        {
            var subcommands = new[] { "list", "send", "recall" };

            return CompletionResult.FromHintOptions(
                subcommands,
                "Subcommand — list/send/recall");
        }

        if (args.Length == 2)
        {
            var subcommand = args[0].ToLowerInvariant();
            if (subcommand is "send" or "recall")
            {
                var armoryKeys = GetArmoryKeys(entMan).ToArray();
                return CompletionResult.FromHintOptions(
                    armoryKeys,
                    "Armory to send or recall.");
            }
        }

        return CompletionResult.Empty;
    }

    private static IEnumerable<string> GetArmoryKeys(IEntityManager entMan)
    {
        var query = entMan.EntityQueryEnumerator<AlertArmoryStationComponent>();
        if (query.MoveNext(out _, out var comp))
        {
            foreach (var key in comp.Grids.Keys)
                yield return key;
        }
    }
}