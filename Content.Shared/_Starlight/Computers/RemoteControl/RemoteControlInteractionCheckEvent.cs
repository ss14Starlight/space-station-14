namespace Content.Shared._Starlight.Computers.RemoteControl;

[ByRefEvent]
public struct RemoteControlInteractionCheckEvent(EntityUid actor, EntityUid target, string permission = "body.interact")
{
    public readonly EntityUid Actor = actor;
    public readonly EntityUid Target = target;
    public readonly string Permission = permission;
    public EntityUid? RemoteEntity;
    public bool Allowed;
}
