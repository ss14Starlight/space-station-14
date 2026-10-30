// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Administration.Components;
using Content.Shared.Examine;
using Content.Shared.Ghost;

namespace Content.Shared.Trigger.Systems;
public sealed partial class TriggerSystem
{
    bool IsGhostAndNotAdminGhost(ExaminedEvent args)
    {
        if (TryComp<AdminGhostComponent>(args.Examiner, out var admin))
        {
            if (admin.HiddenFromNonAdminGhosts)
                return true;
        } else if (TryComp<GhostComponent>(args.Examiner, out var _))
        {
            return true;
        }

        return false;
    }
}
