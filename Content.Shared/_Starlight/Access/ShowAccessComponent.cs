using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Access;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShowAccessComponent : Component
{
    /// Entity must have <see cref="Item.ItemComponent"/> to work, otherwise work with anything.
    [DataField] public bool ItemsOnly = true;

    /// Lets you view access on an entity holding a PDA/ID by examining them directly.
    [DataField] public bool SeeIdHolder;

    [DataField] public string ExamineLocId = "show-access-examined";
}
