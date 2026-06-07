using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Fax;

[Prototype]
public sealed partial class FaxGroupPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)] public int Order { get; private set; } = int.MaxValue;

    [DataField(required: true)] public string Name { get; private set; } = string.Empty;

    [DataField(required: true)] public Color Color { get; private set; } = Color.White;

    /// <summary>
    /// Whether this fax group is selectable on any normal fax.
    /// </summary>
    [DataField]
    public bool Selectable { get; private set; } = true;

    /// <summary>
    /// Whether this fax group is selectable on emagged faxes. (Only relevant when SelectableDefault is false)
    /// </summary>
    [DataField]
    public bool SelectableEmagged { get; private set; } = false;

}
