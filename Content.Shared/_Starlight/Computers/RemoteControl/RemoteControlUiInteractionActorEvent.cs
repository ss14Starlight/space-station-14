namespace Content.Shared._Starlight.Computers.RemoteControl;

[ByRefEvent]
public struct RemoteControlUiInteractionActorEvent(EntityUid controller, EntityUid target, Enum uiKey, bool isOpenAttempt)
{
    public readonly EntityUid Controller = controller;
    public readonly EntityUid Target = target;
    public readonly Enum UiKey = uiKey;
    public readonly bool IsOpenAttempt = isOpenAttempt;
    public EntityUid? Actor;
}
