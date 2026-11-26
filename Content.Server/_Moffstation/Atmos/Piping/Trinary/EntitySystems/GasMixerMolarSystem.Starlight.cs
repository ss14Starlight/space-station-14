using Content.Server._Moffstation.Atmos.Piping.Trinary.Components;
using Content.Shared.Atmos.Piping.Trinary.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._Moffstation.Atmos.Piping.Trinary.EntitySystems;

#region Starlight
public sealed partial class GasMixerMolarSystem
{
    [SubscribeLocalEvent]
    private void OnMixerUiOpened(Entity<GasMixerMolarComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(GasMixerUiKey.Key))
            DirtyUi(ent);
    }
}
#endregion
