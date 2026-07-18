using System.Linq;
using Content.Client.PDA;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Clothing.Systems;

// All valid items for chameleon are calculated on client startup and stored in dictionary.
public sealed partial class ChameleonClothingSystem : SharedChameleonClothingSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChameleonClothingComponent, AfterAutoHandleStateEvent>(HandleState);

        PrepareAllVariants();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnProtoReloaded);
    }

    private void OnProtoReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            PrepareAllVariants();
    }

    private void HandleState(EntityUid uid, ChameleonClothingComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(uid, component);
    }

    protected override void UpdateSprite(EntityUid uid, EntityPrototype proto)
    {
        base.UpdateSprite(uid, proto);
        if (TryComp(uid, out SpriteComponent? sprite)
            && proto.TryComp(out SpriteComponent? _, Factory))
        {
            // Prototype components have no Owner; spawn a temporary copy to CopySprite from.
            var spriteEntity = Spawn(proto.ID, MapCoordinates.Nullspace);
            if (TryComp(spriteEntity, out SpriteComponent? otherSprite))
                _sprite.CopySprite((spriteEntity, otherSprite), (uid, sprite));
            QueueDel(spriteEntity);
        }

        // Edgecase for PDAs to include visuals when UI is open
        if (TryComp(uid, out PdaBorderColorComponent? borderColor)
            && proto.TryComp(out PdaBorderColorComponent? otherBorderColor, Factory))
        {
            borderColor.BorderColor = otherBorderColor.BorderColor;
            borderColor.AccentHColor = otherBorderColor.AccentHColor;
            borderColor.AccentVColor = otherBorderColor.AccentVColor;
        }
    }
}
