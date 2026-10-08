using Content.Shared._DEN.QuickConstruction.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DEN.QuickConstruction.Components;
/// <summary>
/// This component allows items to be interacted with to open a quick construction radial menu
/// containing a category of items to construct.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Starlight
public sealed partial class QuickConstructableComponent : Component
{
    [DataField, AutoNetworkedField] // Starlight
    public ProtoId<QuickConstructionCategoryPrototype> Category;
}
