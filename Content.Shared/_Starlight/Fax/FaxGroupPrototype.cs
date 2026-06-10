using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Fax;

/// <summary>
/// Represents a logical group that fax machines can be part of. Each group can have a name, color, and order that are
/// used to visually categorize fax machines and make the ordering predictable.
/// </summary>
[Prototype]
public sealed partial class FaxGroupPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The order of this group. Lower values mean higher up in the list.
    /// </summary>
    [DataField(required: true)]
    public int Order { get; private set; } = int.MaxValue;

    /// <summary>
    /// The group name (gets localized).
    /// </summary>
    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The color of this group.
    /// </summary>
    [DataField(required: true)]
    public Color Color { get; private set; } = Color.White;

    /// <summary>
    /// Whether this fax group is selectable on any normal fax.
    /// </summary>
    [DataField]
    public bool Selectable { get; private set; } = true;

    /// <summary>
    /// Whether this fax group is selectable on emagged faxes. (Only relevant when Selectable is false).
    /// </summary>
    [DataField]
    public bool SelectableEmagged { get; private set; }

}
