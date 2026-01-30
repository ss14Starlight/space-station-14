using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// Adds the given tags when the item is equipped.
/// Will remove the tags (if they didn't already exist) when the clothing is removed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AddTagsOnClothingEquipComponent : Component
{
    /// <summary>
    /// A list of tags to add to the entity that equips clothing with this component
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TagPrototype>> TagsToAdd = [];

    /// <summary>
    /// If true, will remove all the tags that were added when the item was equipped.
    /// If false, will not remove any tags when the clothing is unequipped.
    /// </summary>
    [DataField]
    public bool RemoveTagsOnUnequip = true;

    /// <summary>
    /// Tags that were added to the equipped entity. These will be removed when the clothing is unequipped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TagPrototype>> AddedTags = [];
}