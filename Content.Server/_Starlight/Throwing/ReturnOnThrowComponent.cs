namespace Content.Server._Starlight.Throwing;

[RegisterComponent]
public sealed partial class ReturnOnThrowComponent : Component
{
    [DataField]
    public float ReturnSpeed = 3f;

    public EntityUid? ReturnTarget;
}
