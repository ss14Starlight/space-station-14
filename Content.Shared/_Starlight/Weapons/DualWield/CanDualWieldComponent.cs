namespace Content.Shared._Starlight.Weapons.DualWield;

/// <summary>
/// Marks a gun as light enough to be dual-wielded.
/// Add this to pistols and SMGs. Without it the dual-wield verb
/// will be shown but disabled with a "too heavy" message.
/// </summary>
[RegisterComponent]
public sealed partial class CanDualWieldComponent : Component
{
    /// <summary>
    /// Extra spread (in degrees) added to both MinAngle and MaxAngle
    /// while this gun is being actively dual-wielded.
    /// Use high values (20-45°) for disabler-type weapons.
    /// </summary>
    [DataField]
    public float DualWieldInaccuracyPenalty = 0f;
}
