namespace Content.Shared._Starlight.UI;

[RegisterComponent]
public sealed partial class ActivateUiOnStrappedComponent : Component
{
    [DataField(required: true)]
    public Enum Key;
}
