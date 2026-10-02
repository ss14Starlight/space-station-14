using Content.Shared.Access.Systems;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Silicons.StationAi;

namespace Content.Client.Access.Systems;

public sealed partial class JobStatusSystem : SharedJobStatusSystem
{
    // Show job icons if entity is in camera view (only relevant for AI viewers) OR they have active suit sensors.
    private bool CanSeeJobStatus(EntityUid uid)
    {
        if (_player.LocalEntity is not { } localEnt
            || !HasComp<StationAiOverlayComponent>(localEnt)
            || !_vision.IsOutsideCameraViewCached(uid))
        {
            return true;
        }

        foreach (var sensor in EntityQuery<SuitSensorComponent>(true))
        {
            if (sensor.User == uid && sensor.Mode == SuitSensorMode.SensorCords)
                return true;
        }

        return false;
    }
}
