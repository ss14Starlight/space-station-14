using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Roles;

public sealed partial class AntagPrototype
{
    /// <summary>
    /// Whether this prototype represents a generic antagonist category.
    /// Not displayed in character menu if a specific antagonist role is present.
    /// </summary>
    [DataField]
    public bool IsGeneric { get; private set; }

    /// <summary>
    /// Shown in the character editor in place of the character, for antags that become something else.
    /// </summary>
    [DataField]
    public EntProtoId? PreviewEntity { get; private set; }
}
