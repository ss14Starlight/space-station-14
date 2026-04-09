using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Body.Components;
using Content.Shared.Repairable;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    private void InitializeWeld()
    {
        SubscribeLocalEvent<IPCWeldClosesWoundsComponent, RepairDoAfterEvent>(OnIPCRepaired);
    }

    private void OnIPCRepaired(Entity<IPCWeldClosesWoundsComponent> ent, ref RepairDoAfterEvent args)
    {
        if (args.Cancelled || !TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream)) return;

        _bloodstream.TryModifyBleedAmount((ent, bloodstream), ent.Comp.BloodlossModifier);
    }
}
