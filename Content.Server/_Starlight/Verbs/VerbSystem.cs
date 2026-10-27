using Content.Server._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.BodyControl;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Verbs;

// ReSharper disable CheckNamespace
namespace Content.Server.Verbs;

public sealed partial class VerbSystem
{
    [Dependency] private RemoteControlConsoleSystem _remoteControl = default!;
    [Dependency] private BodyControlHandContextSystem _verbBodyControl = default!;

    private bool WithControlledVerbRequest(EntityUid attached, RequestServerVerbsEvent args, EntitySessionEventArgs eventArgs)
        => _verbBodyControl.WithController(attached, null, false, () => HandleVerbRequest(args, eventArgs));

    protected override EntityUid? ResolveVerbUser(EntityUid attachedEntity)
    {
        if (!_remoteControl.TryGetControlledEntity(attachedEntity, out var remoteEntity))
            return attachedEntity;

        return _remoteControl.CanRemoteControlledEntityAct(remoteEntity) ? remoteEntity : null;
    }
}
