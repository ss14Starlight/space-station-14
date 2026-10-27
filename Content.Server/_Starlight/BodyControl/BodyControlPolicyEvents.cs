namespace Content.Server._Starlight.BodyControl;

/// <summary>Allows a provider to keep a message on its original source instead of speaking through the controlled body.</summary>
public sealed class BodyControlSpeechRelayEvent(EntityUid source, string message) : EntityEventArgs
{
    public readonly EntityUid Source = source;
    public readonly string Message = message;
    public bool Cancelled;
}

/// <summary>Identifies a controller's personal interface that is independent of access to the body's inventory.</summary>
public sealed class BodyControlPersonalInterfaceEvent(EntityUid actor, EntityUid target, Enum uiKey) : EntityEventArgs
{
    public readonly EntityUid Actor = actor;
    public readonly EntityUid Target = target;
    public readonly Enum UiKey = uiKey;
    public bool Personal;
}
