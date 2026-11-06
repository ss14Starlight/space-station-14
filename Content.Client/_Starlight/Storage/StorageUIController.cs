// ReSharper disable CheckNamespace
using Content.Client.Hands.Systems;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared.Hands.EntitySystems;

namespace Content.Client.UserInterface.Systems.Storage;

public sealed partial class StorageUIController
{
    public EntityUid? GetActiveStorageHandItem()
    {
        var remoteEntity = EntityManager.System<RemoteControlInterface>().ControlledEntity;
        return remoteEntity is { } entity
            ? EntityManager.System<SharedHandsSystem>().GetActiveItem(entity)
            : EntityManager.System<HandsSystem>().GetActiveHandEntity();
    }
}
