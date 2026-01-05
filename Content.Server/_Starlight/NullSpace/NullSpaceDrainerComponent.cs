namespace Content.Server._Starlight.Shadekin;

[RegisterComponent]
public sealed partial class NullSpaceDrainerComponent : Component
{
    [DataField]
    public EntityUid? Target;
}