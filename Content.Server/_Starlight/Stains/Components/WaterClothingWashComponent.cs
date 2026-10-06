using Content.Server._Starlight.Stains.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Stains.Components;

/// <summary>
/// Gradually removes stains from clothing worn by entities standing on this tile.
/// </summary>
[RegisterComponent, Access(typeof(WaterClothingWashSystem))]
public sealed partial class WaterClothingWashComponent : Component
{
    /// <summary>
    /// Time that an entity must spend in water between washing updates.
    /// </summary>
    [DataField]
    public float WashInterval = 1f;

    /// <summary>
    /// Solution volume removed from each worn item per washing update.
    /// </summary>
    [DataField]
    public FixedPoint2 WashAmount = 0.1f;
}
