using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light.Components;

namespace Content.Shared._Starlight.Xenobiology.MiscItems;

public sealed class YellowSlimeExtractLightSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedPointLightSystem _sharedPointLightSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<YellowSlimeExtractLightComponent, ItemToggledEvent>(OnLightToggled);
    }

    private void OnLightToggled(Entity<YellowSlimeExtractLightComponent> ent, ref ItemToggledEvent args)
    {
        if (ent.Comp.IsLarge)
        {
            _sharedPointLightSystem.SetRadius(ent, 2);
            _sharedPointLightSystem.SetEnergy(ent, 1);
        }
        else
        {
            _sharedPointLightSystem.SetRadius(ent, 8);
            _sharedPointLightSystem.SetEnergy(ent, 5);
        }
        Dirty(ent.Owner, ent.Comp);

        ent.Comp.IsLarge = !ent.Comp.IsLarge;
    }
}