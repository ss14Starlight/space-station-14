namespace Content.Shared.Roles;

public sealed partial class AntagPrototype
{
    /// <summary>
    /// Whether this prototype represents a generic antagonist category.
    /// Not displayed in character menu if a specific antagonist role is present.
    /// </summary>
    [DataField]
    public bool IsGeneric { get; private set; }
}
