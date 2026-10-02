using Content.Shared.Clothing;
using Content.Shared._Starlight.VentCrawl.Components;

namespace Content.Shared._Starlight.VentCrawl.EntitySystems;

public sealed partial class SharedVentCrawlSystem
{
    public void InitializeClothing()
    {
        SubscribeLocalEvent<VentCrawlClothingComponent, ClothingGotEquippedEvent>(OnClothingEquip);
        SubscribeLocalEvent<VentCrawlClothingComponent, ClothingGotUnequippedEvent>(OnClothingUnequip);
    }

    private void OnClothingEquip(Entity<VentCrawlClothingComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        EnsureComp<VentCrawlerComponent>(args.Wearer);
    }

    private void OnClothingUnequip(Entity<VentCrawlClothingComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        RemComp<VentCrawlerComponent>(args.Wearer);
    }
}
