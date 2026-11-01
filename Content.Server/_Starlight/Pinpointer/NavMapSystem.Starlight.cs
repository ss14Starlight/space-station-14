// ReSharper disable CheckNamespace
using Content.Shared.Pinpointer;

namespace Content.Server.Pinpointer;

public sealed partial class NavMapSystem
{
    /// <summary>
    /// Refreshes a beacon's entry in the grid's nav map data.
    /// </summary>
    public void RefreshNavMapBeacon(Entity<NavMapBeaconComponent> ent)
    {
        UpdateNavMapBeaconData(ent.Owner, ent.Comp);
    }
}
