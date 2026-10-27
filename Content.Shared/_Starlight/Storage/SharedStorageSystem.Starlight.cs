// ReSharper disable CheckNamespace

using Content.Shared._Starlight.Storage;
using Content.Shared._Starlight.BodyControl;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    // Preserve the requester's hand across the existing storage actor-to-body resolver.
    private bool WithRemoteHand(EntitySessionEventArgs args, Action command)
    {
        return args.SenderSession.AttachedEntity is { } actor
               && EntityManager.System<BodyControlHandContextSystem>()
                   .WithController(actor, "body.inventory", true, command);
    }

    [Dependency] private SharedPrivateStorageSystem _privateStorage = default!;

    private partial bool HandleStarlightStrippableStorageVerb(
        EntityUid uid,
        StorageComponent component,
        GetVerbsEvent<ActivationVerb> args) =>
        _privateStorage.TryAddStrippableStorageVerb(uid, component, args);
}
