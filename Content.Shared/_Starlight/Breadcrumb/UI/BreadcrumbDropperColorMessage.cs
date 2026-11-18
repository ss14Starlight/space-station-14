using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Breadcrumb.UI;

/// <summary>
/// Sent when the player picks a preset color and hits submit in the UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class BreadcrumbDropperColorMessage(string color) : BoundUserInterfaceMessage
{
    public string Color = color;
}
