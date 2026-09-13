using Content.Shared._Starlight.Construction;

// ReSharper disable CheckNamespace
// Partial of the upstream ConstructionSystem, so it has to share its namespace.
namespace Content.Client.Construction;

public sealed partial class ConstructionSystem
{
    /// <summary>
    /// Asks the server to instantly finish every construction ghost this client has placed.
    /// </summary>
    /// <returns>Number of ghosts that were sent to the server.</returns>
    public int DebugFinishAllGhosts()
    {
        var ghosts = new List<DebugConstructionGhost>();

        foreach (var ghost in _ghosts.Values)
        {
            if (!TryComp<ConstructionGhostComponent>(ghost, out var comp) || comp.Prototype is null)
                continue;

            var xform = Transform(ghost);

            ghosts.Add(new DebugConstructionGhost
            {
                Location = GetNetCoordinates(xform.Coordinates),
                PrototypeName = comp.Prototype.ID,
                Angle = xform.LocalRotation,
                Ack = comp.GhostId,
            });
        }

        if (ghosts.Count == 0)
            return 0;

        RaiseNetworkEvent(new DebugFinishConstructionGhostsMessage(ghosts));
        return ghosts.Count;
    }
}
