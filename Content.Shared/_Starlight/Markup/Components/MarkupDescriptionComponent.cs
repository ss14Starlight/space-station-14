using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Markup.Components;

/// Pushes custom markup text to the description of the entity. Allows for using richtext tags.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(MarkupTextSystem))]
public sealed partial class MarkupDescriptionComponent : Component
{
    /// <summary>
    /// Text that will be appended to the description of an entity on examine.
    /// Is a dictionary so an ID can be assigned to it, primarily for toolshed.
    /// </summary>
    [DataField, AutoNetworkedField] public Dictionary<string, (int, string)> Texts = [];
}
