// ReSharper disable once CheckNamespace
namespace Content.Shared.Doors.Components;

public sealed partial class FirelockComponent
{
    /// <summary>
    /// Determines if the pressure wire is cut on the firelock door
    /// </summary>
    [DataField] public bool PressureWireCut;
    /// <summary>
    /// Determines if the heat wire is cut on the firelock door
    /// </summary>
    [DataField] public bool HeatWireCut;
}
