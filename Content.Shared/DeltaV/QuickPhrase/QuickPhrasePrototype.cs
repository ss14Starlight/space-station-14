using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.DeltaV.QuickPhrase;

[Prototype] // Starlight Edit: Removed ``("quickPhrase)``
public sealed partial class QuickPhrasePrototype : IPrototype, IInheritingPrototype
{
    /// <summary>
    /// The "in code name" of the object. Must be unique.
    /// </summary>
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The prototype we inherit from.
    /// </summary>
    [ViewVariables]
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<QuickPhrasePrototype>))]
    public string[]? Parents { get; private set; } // Starlight Edit: Added Setter

    [ViewVariables]
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; } // Starlight Edit: Added Setter

    /// <summary>
    /// The phrase that this prototype represents.
    /// </summary>
    [DataField]
    public LocId Text = string.Empty;

    /// <summary>
    /// Determines how the phrase is sorted in the UI.
    /// </summary>
    [DataField]
    public LocId Group = "aac-group-general"; // Starlight Edit: Localized group label for fallback

    /// <summary>
    /// Starlight: Sort order of the group within its tab. Lower values are shown first and ties are sorted by name.
    /// </summary>
    [DataField]
    public int GroupOrder;

    /// <summary>
    /// The tab in the UI that this phrase falls under.
    /// </summary>
    [DataField]
    public string Tab = string.Empty;

    /// <summary>
    /// Color of button in UI.
    /// </summary>
    [DataField]
    public string StyleClass = string.Empty;
}
