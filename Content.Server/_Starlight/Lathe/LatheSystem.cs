// ReSharper disable CheckNamespace
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Power;
using Robust.Shared.GameObjects;

namespace Content.Server.Lathe;

public sealed partial class LatheSystem
{
    [SubscribeLocalEvent]
    private void OnSiloMaterialChanged(Entity<OreSiloComponent> ent, ref MaterialAmountChangedEvent args)
        => NotifySiloClients(ent);

    [SubscribeLocalEvent]
    private void OnSiloPowerChanged(Entity<OreSiloComponent> ent, ref PowerChangedEvent args)
        => NotifySiloClients(ent);

    private void NotifySiloClients(Entity<OreSiloComponent> ent)
    {
        var ev = new MaterialAmountChangedEvent();
        foreach (var client in ent.Comp.Clients)
        {
            RaiseLocalEvent(client, ref ev);
        }
    }
}
