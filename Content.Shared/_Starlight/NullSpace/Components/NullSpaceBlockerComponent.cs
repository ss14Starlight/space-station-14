namespace Content.Shared._Starlight.NullSpace;

/// <summary>
/// Will block the ability to "Phase"
/// </summary>
[RegisterComponent]
public sealed partial class NullSpaceBlockerComponent : Component
{
    /// <summary>
    /// Will force Unphase any ent that touches it.
    /// </summary>
    [DataField]
    public bool UnphaseOnCollide = true;
}
