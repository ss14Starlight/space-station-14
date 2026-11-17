namespace Content.Shared._Starlight.Roles.Components;

/// <summary>
/// For job entities that get their own name rather than the profile's, like a K9's dog name: that name (with any
/// identifier, e.g. "Bella (317)") becomes the character's name, used for their ghost and the round end summary.
/// </summary>
[RegisterComponent]
public sealed partial class UseEntityNameAsCharacterNameComponent : Component;
