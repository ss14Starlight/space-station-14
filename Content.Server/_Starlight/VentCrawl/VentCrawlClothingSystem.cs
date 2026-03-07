using Content.Shared.Clothing;
using Content.Shared.VentCrawl.Components;
using Content.Shared.VentCrawl;

namespace Content.Server.VentCrawl;

public sealed class VentCrawlClothingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlClothingComponent, ClothingGotEquippedEvent>(OnClothingEquip);
        SubscribeLocalEvent<VentCrawlClothingComponent, ClothingGotUnequippedEvent>(OnClothingUnequip);
    }

    private void OnClothingEquip(Entity<VentCrawlClothingComponent> ent, ref ClothingGotEquippedEvent args)
    {
        AddComp<VentCrawlerComponent>(args.Wearer);
    }

    private void OnClothingUnequip(Entity<VentCrawlClothingComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemComp<VentCrawlerComponent>(args.Wearer);
    }
}
