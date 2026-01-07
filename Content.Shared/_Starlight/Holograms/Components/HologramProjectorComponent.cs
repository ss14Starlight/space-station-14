using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Holograms;

/// <summary>
/// Marks an entity as capable of projecting holograms.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HologramProjectorComponent : Component
{
    /// <summary>
    ///     The maximum range from this projector that holograms can be before they're returned.
    /// </summary>
    /// <remarks>
    ///     Note that making this number larger than PVS is highly inadvisable, as the client will be stuck predicting the Hologram returning while the server confirms that they do not.
    /// </remarks>
    [DataField("projectorRange")]
    public float ProjectorRange = 14f;

    /// <summary>
    ///     The tile offset of the projector effect for this projector for each direction.
    /// </summary>
    [DataField("effectOffsets")]
    public Dictionary<Direction, Vector2> EffectOffsets { get; set; } = new() { { Direction.North, Vector2.Zero }, { Direction.East, Vector2.Zero }, { Direction.South, Vector2.Zero }, { Direction.West, Vector2.Zero } };

    /// <summary>
    ///     Whether this projector is currently active and working.
    /// </summary>
    [DataField("isActive")]
    public bool IsActive = true;
}
