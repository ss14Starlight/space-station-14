using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Roles;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype]
public sealed partial class AntagCategoryPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// The name LocId of the antag catagory that will be displayed in the various menus.
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    /// <summary>
    /// A description LocId to display in the character menu as an explanation of the faction
    /// </summary>
    [DataField(required: true)]
    public LocId Description = string.Empty;

    /// <summary>
    /// The accent color of this category.
    /// </summary>
    [DataField(required: true)]
    public Color AccentColor;

    /// <summary>
    /// The antags that belong to this category
    /// </summary>
    [DataField]
    public List<ProtoId<AntagPrototype>> Antags = new();

    /// <summary>
    /// Subcategories for this category. Categories will be nested inside this one.
    /// </summary>
    [DataField]
    public List<ProtoId<AntagCategoryPrototype>>? SubCategories = new();

    /// <summary>
    /// Categories with a heigher weight will be added first (and appear the lowest) in the UI.
    /// </summary>
    [DataField]
    public int Priority = 1;

    /// <summary>
    /// Whether this category should be expanded by default
    /// </summary>
    [DataField]
    public bool DefaultExpanded = true;

    /// <summary>
    /// Antags which belong to no category will be assigned to this one automatically
    /// </summary>
    [DataField]
    public bool Default;
}
