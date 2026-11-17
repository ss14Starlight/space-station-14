using Content.Server.Atmos.Piping.Trinary.Components;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.Atmos.Piping.Trinary.EntitySystems;

#region Starlight
public sealed partial class GasMixerSystem
{
    [SubscribeLocalEvent]
    private void OnMixerUiOpened(Entity<GasMixerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(GasMixerUiKey.Key))
            DirtyUI(ent.Owner, ent.Comp);
    }
}
#endregion
