using Content.Shared.Body.Part;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnProgressUpdated(Entity<SurgeryProgressComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<BodyPartComponent>(ent, out var part))
            return;

        var uiOwner = part.Body ?? ent.Owner;
        if (_ui.TryGetOpenUi<SurgeryBui>(uiOwner, SurgeryUIKey.Key, out var bui))
            bui.RefreshUI();
    }
}
