using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Shuttles.Systems;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Shuttles;

[ToolshedCommand(Name = "epod")]
[AdminCommand(AdminFlags.Round)]
public sealed partial class EscapePodCommand : ToolshedCommand
{
    [Dependency] private IAdminLogManager _log = default!;

    private EmergencyShuttleSystem? _emergencyShuttle;

    /// <summary>
    /// Unlock every escape pod console.
    /// </summary>
    [CommandImplementation("unlockall")]
    public void UnlockAllEscapePods(IInvocationContext ctx)
    {
        _emergencyShuttle ??= GetSys<EmergencyShuttleSystem>();
        var unlocked = _emergencyShuttle.UnlockAllEscapePods();
        _log.Add(LogType.AdminCommands,
            $"{SharedCommandHelpers.PlayerNameOrServer(ctx)} unlocked {unlocked} escape pod consoles.");
        ctx.WriteLine($"Unlocked {unlocked} escape pod consoles.");
    }

    /// <summary>
    /// Immediately begin launching every escape pod.
    /// </summary>
    [CommandImplementation("launchall")]
    public void LaunchAllEscapePods(IInvocationContext ctx)
    {
        _emergencyShuttle ??= GetSys<EmergencyShuttleSystem>();
        var launched = _emergencyShuttle.LaunchAllEscapePods();
        _log.Add(LogType.AdminCommands,
            $"{SharedCommandHelpers.PlayerNameOrServer(ctx)} requested launch for {launched} escape pods.");
        ctx.WriteLine($"Requested launch for {launched} escape pods.");
    }
}
