namespace Content.Server._Starlight.Clothing.Components;

/// <summary>
/// Marker added to the entity wearing capacitor gloves.
/// Enables EMP events to propagate from the wearer to the glove battery.
/// </summary>
[RegisterComponent]
public sealed partial class WornCapacitorGlovesComponent : Component
{
    /// <summary>The gloves entity that added this component.</summary>
    [DataField]
    public EntityUid GlovesUid;
}
