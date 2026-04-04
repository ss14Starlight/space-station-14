namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent]
public sealed partial class WrappedComponent : Component
{
    [DataField]
    public TimeSpan UnWrapTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan SelfUnWrapTime = TimeSpan.FromSeconds(15);
}
