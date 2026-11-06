// ReSharper disable CheckNamespace

using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Map;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    internal bool IsFTLFree(EntityUid shuttleUid, EntityCoordinates coordinates, Angle angle)
    {
        List<ShuttleExclusionObject>? exclusions = null;
        GetExclusions(ref exclusions);
        return _shuttle.FTLFree(shuttleUid, coordinates, angle, exclusions);
    }
}
