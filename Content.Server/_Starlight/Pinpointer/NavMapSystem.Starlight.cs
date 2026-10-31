using Content.Shared.Pinpointer;

namespace Content.Server.Pinpointer;

public sealed partial class NavMapSystem
{
    /// <summary>
    /// Refreshes a beacon's entry in the grid's nav map data.
    /// </summary>
    public void RefreshNavMapBeacon(EntityUid uid, NavMapBeaconComponent? component = null)
    {
        if (!Resolve(uid, ref component, logMissing: false))
            return;

        UpdateNavMapBeaconData(uid, component);
    }
}
