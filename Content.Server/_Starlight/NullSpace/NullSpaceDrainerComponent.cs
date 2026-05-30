namespace Content.Server._Starlight.Shadekin;

[RegisterComponent]
public sealed partial class NullSpaceDrainerComponent : Component
{
    [DataField]
    public EntityUid? Target;

    /// <summary>
    /// Points drained by energy/sec
    /// </summary>
    [DataField]
    public int Points = 100;
}
