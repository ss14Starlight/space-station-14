namespace Content.Shared._Starlight.Computers.RemoteControl;

[ByRefEvent]
public struct RemoteControlInteractionCheckEvent(EntityUid actor, EntityUid target)
{
    public readonly EntityUid Actor = actor;
    public readonly EntityUid Target = target;
    public EntityUid? RemoteEntity;
    public bool Allowed;
}
