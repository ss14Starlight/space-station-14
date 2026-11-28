// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Storage;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    [Dependency] private SharedRemoteControlConsoleSystem _remoteControl = default!;
    [Dependency] private SharedPrivateStorageSystem _privateStorage = default!;

    public void OpenRemoteStorageUiMirror(EntityUid storage, EntityUid controller)
    {
        var wasNestedCheck = _nestedCheck;
        _nestedCheck = true;
        try
        {
            UI.OpenUi((storage, null), StorageComponent.StorageUiKey.Key, controller);
        }
        finally
        {
            _nestedCheck = wasNestedCheck;
        }
    }

    private partial bool HandleStarlightStrippableStorageVerb(
        EntityUid uid,
        StorageComponent component,
        GetVerbsEvent<ActivationVerb> args) =>
        _privateStorage.TryAddStrippableStorageVerb(uid, component, args);
}
