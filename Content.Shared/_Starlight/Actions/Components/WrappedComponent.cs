using Content.Shared.Alert;

namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent]
public sealed partial class WrappedComponent : Component
{
    public EntityUid? Holder = null;
}

public sealed partial class UnWrapAlertEvent : BaseAlertEvent;
