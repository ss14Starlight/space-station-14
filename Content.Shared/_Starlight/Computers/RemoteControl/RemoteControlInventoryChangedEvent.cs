namespace Content.Shared._Starlight.Computers.RemoteControl;

public sealed class RemoteControlInventoryChangedEvent(EntityUid actor) : EntityEventArgs
{
    public readonly EntityUid Actor = actor;
}
