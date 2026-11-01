using Content.Shared.Store.Components;

// ReSharper disable CheckNamespace
namespace Content.Client.Store;

public sealed partial class StoreSystem
{
    // Store listings, balances, and display options are authoritative on the server.
    public override void UpdateUserInterface(EntityUid? user, EntityUid store, StoreComponent? component = null)
    {
    }
}
