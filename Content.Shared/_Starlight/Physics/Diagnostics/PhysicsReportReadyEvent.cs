using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Physics.Diagnostics;

/// <summary>
/// Sends a finished physics report to the admin who ran the command, so their client can save it.
/// </summary>
[Serializable, NetSerializable]
public sealed class PhysicsReportReadyEvent(string report) : EntityEventArgs
{
    public readonly string Report = report;
}
