namespace Content.Shared._Starlight.Interaction.Components;

/// <summary>
/// Activates the worn item carrying this component when its wearer is hugged.
/// </summary>
[RegisterComponent]
public sealed partial class ActivateOnWearerHuggedComponent : Component
{
    /// <summary>
    /// Inventory slot the item must be worn in for a hug to activate it.
    /// Must match a slot in the wearer's inventory template (see
    /// Resources/Prototypes/InventoryTemplates/). Humanoid slots are:
    /// back, belt, ears, eyes, gloves, head, id, jumpsuit, mask, misc,
    /// neck, outerClothing, pocket1, pocket2, shoes, suitstorage, suitstorage2.
    /// Other species use their own templates and may have additional slots.
	/// Defaults to "head".
    /// </summary>
    [DataField]
    public string Slot = "head";
}
