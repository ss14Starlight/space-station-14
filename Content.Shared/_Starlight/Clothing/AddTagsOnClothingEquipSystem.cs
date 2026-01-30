using Content.Shared.Clothing.Components;
using Content.Shared.Tag;

namespace Content.Shared.Clothing.EntitySystems;

public sealed class AddTagsOnClothingEquipSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddTagsOnClothingEquipComponent, ClothingGotEquippedEvent>(OnClothingEquip);
        SubscribeLocalEvent<AddTagsOnClothingEquipComponent, ClothingGotUnequippedEvent>(OnClothingUnequip);
    }
    
    private void OnClothingEquip(Entity<AddTagsOnClothingEquipComponent> ent, ref ClothingGotEquippedEvent args)
    {
        // This is not perfect, if the tag already exists but is temporary this will skip it.
        foreach (var tag in ent.Comp.TagsToAdd)
        {
            if (_tag.AddTag(args.Wearer, tag))
                ent.Comp.AddedTags.Add(tag);
        }

        Dirty(ent);
    }
    
    private void OnClothingUnequip(Entity<AddTagsOnClothingEquipComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (ent.Comp.AddedTags.Count == 0 || !ent.Comp.RemoveTagsOnUnequip)
            return;

        // Remove all added tags - just assume they are all still there. If not then too bad!
        _tag.RemoveTags(args.Wearer, ent.Comp.AddedTags);
        ent.Comp.AddedTags.Clear();

        Dirty(ent);
    }
}