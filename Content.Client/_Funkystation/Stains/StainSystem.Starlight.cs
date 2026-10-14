using Content.Shared._Funkystation.Stains.Components;
using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;

namespace Content.Client._Funkystation.Stains;

public sealed partial class StainSystem : SharedStainSystem
{
    [Dependency] private SharedItemSystem _items = null!;
    [Dependency] private IConfigurationManager _cfg = null!;

    private bool _showClothingStains;

    private void OnShowClothingStainsChanged(bool show)
    {
        _showClothingStains = show;

        // Refresh existing item icons, in-hand sprites and worn clothing when the option is applied.
        var query = EntityQueryEnumerator<StainableComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (TryComp<AppearanceComponent>(uid, out var appearance))
                EntityManager.System<AppearanceSystem>().QueueUpdate(uid, appearance);

            _items.VisualsChanged(uid);
        }
    }

    private void OnMaskStateChanged(Entity<MaskComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (HasComp<StainableComponent>(ent.Owner))
            _items.VisualsChanged(ent.Owner);
    }
}
