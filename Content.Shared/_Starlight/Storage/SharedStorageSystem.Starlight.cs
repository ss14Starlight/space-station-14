// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    [Dependency] private SharedPrivateStorageSystem _privateStorage = default!;

    private partial bool HandleStarlightStrippableStorageVerb(
        EntityUid uid,
        StorageComponent component,
        GetVerbsEvent<ActivationVerb> args) =>
        _privateStorage.TryAddStrippableStorageVerb(uid, component, args);
}
