using Content.Server._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Computers.RemoteControl;

// ReSharper disable CheckNamespace
namespace Content.Server.Verbs;

public sealed partial class VerbSystem
{
    [Dependency] private RemoteControlConsoleSystem _remoteControl = default!;

    protected override EntityUid? ResolveVerbUser(EntityUid attachedEntity)
    {
        if (!_remoteControl.TryGetControlledEntity(attachedEntity, out var remoteEntity))
            return attachedEntity;

        return remoteEntity;
    }
}
