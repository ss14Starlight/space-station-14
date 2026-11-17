// ReSharper disable CheckNamespace
namespace Content.Shared.Power.Components;

public sealed partial class ActivatableUIRequiresPowerComponent
{
    /// <summary>
    /// Allows a power-cell requirement to substitute for APC power when opening the UI.
    /// </summary>
    [DataField] public bool AllowPowerCellFallback;
}
