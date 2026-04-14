namespace Content.Shared._Starlight.NullSpace;

/// <summary>
/// Will block and effects nullspace entities.
/// </summary>
[RegisterComponent]
public sealed partial class NullSpaceBlockerComponent : Component
{
    /// <summary>
    /// Should it BypassPVS?
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool BypassPVS = false;

    /// <summary>
    /// Will force Unphase any ent that touches it.
    /// </summary>
    [DataField]
    public bool UnphaseOnCollide = true;
}
