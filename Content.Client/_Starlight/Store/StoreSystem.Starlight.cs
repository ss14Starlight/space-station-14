using Content.Shared.Store.Components;

// ReSharper disable CheckNamespace
namespace Content.Client.Store;

public sealed partial class StoreSystem
{
    /// <summary>
    /// Intentionally does nothing because the server refreshes store listings, balances, and display options.
    /// </summary>
    public override void UpdateUserInterface(EntityUid? user, EntityUid store, StoreComponent? component = null)
    {
    }
}
