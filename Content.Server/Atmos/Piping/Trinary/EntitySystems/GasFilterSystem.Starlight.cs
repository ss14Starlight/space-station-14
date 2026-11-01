using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.Atmos.Piping.Trinary.EntitySystems;

#region Starlight
public sealed partial class GasFilterSystem
{
    [SubscribeLocalEvent]
    private void OnFilterUiOpened(Entity<GasFilterComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(GasFilterUiKey.Key))
            DirtyUI(ent.Owner, ent.Comp);
    }
}
#endregion
