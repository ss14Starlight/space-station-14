using Robust.Shared.Console;
using Content.Shared.Administration;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Toolshed;
using Content.Server.Administration;
namespace Content.Server._Starlight.DropPods;

[ToolshedCommand(Name = "drop"), AdminCommand(AdminFlags.Admin)]
internal sealed class DropCommand : ToolshedCommand
{
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IEntityManager entMgr = default!;

    #region drop implementations
    [CommandImplementation("on")]
    public void Execute([PipedArgument] EntityUid who, EntityUid where, Int32 type = 1)
    {
        string proto = type switch // type should be replaced with bsPrototype
        {
            1 => "SpawnSupplyPod",
            2 => "SpawnSupplyPodSecondary",
        };

        var location = entMgr.GetComponent<TransformComponent>(where).Coordinates;
        EntityUid spawnedPod = entMgr.SpawnEntity(proto, location);

        Timer.Spawn(TimeSpan.FromSeconds(entMgr.GetComponent<TimedDespawnComponent>(spawnedPod).Lifetime), () =>
        {
            EntityUid marker = entMgr.SpawnEntity("AITimedSpawner", location); // dummy for tpto
            _console.ExecuteCommand($"tpto {marker.Id} {who.Id}");
            entMgr.DeleteEntity(marker);
        });        
    }
    #endregion
}