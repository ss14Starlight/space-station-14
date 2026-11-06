// ReSharper disable once CheckNamespace
namespace Content.Shared._Moffstation.Roles;

public sealed partial class AntagCategoryPrototype
{
    /// <summary>
    /// Keep individual faction colors when this category does not specify a shared color.
    /// </summary>
    [DataField]
    public bool KeepIndividualColors;

    /// <summary>
    /// Optional shared color for this category's factions in the character menu.
    /// </summary>
    [DataField]
    public Color? FactionColor;
}
