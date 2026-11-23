// ReSharper disable once CheckNamespace
namespace Content.Client.IconSmoothing;

public sealed partial class IconSmoothComponent
{
    [DataField]
    public string StateSuffix = string.Empty;

    /// <summary>
    /// Whether the Linear IconSmooth mode should connect corners for this entity.
    /// </summary>
    [DataField]
    public bool LinearCorners;
}
