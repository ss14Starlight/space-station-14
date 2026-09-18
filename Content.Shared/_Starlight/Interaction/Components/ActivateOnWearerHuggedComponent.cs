namespace Content.Shared._Starlight.Interaction.Components;

/// <summary>
/// Activates the worn item carrying this component when its wearer is hugged.
/// </summary>
[RegisterComponent]
public sealed partial class ActivateOnWearerHuggedComponent : Component
{
    /// <summary>
    /// Inventory slot the item must be worn in for a hug to activate it.
    /// See Resources/Prototypes/InventoryTemplates/ for slots.
    /// </summary>
    [DataField]
    public string Slot = "head";
}
