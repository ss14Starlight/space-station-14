using Robust.Shared.GameObjects;

// ReSharper disable once CheckNamespace
namespace Content.Shared.PowerCell.Components;

public sealed partial class PowerCellSlotComponent
{
    /// <summary>
    /// Whether the maintenance panel must be open before this cell can be ejected.
    /// </summary>
    [DataField]
    public bool RequiresOpenPanelToEject;
}
