using System.Numerics;

namespace Content.Shared._Starlight.CosmicCult.Components;

/// <summary>
/// This is used to apply an offset to the star mark that shows up at cult tier 3. Optional per-direction overrides. Unset directions fall back to Offset.
/// </summary>
[RegisterComponent]
public sealed partial class CosmicStarMarkOffsetComponent : Component
{
    [DataField]
    public Vector2 Offset = Vector2.Zero;

    [DataField]
    public Vector2? South;

    [DataField]
    public Vector2? East;

    [DataField]
    public Vector2? North;

    [DataField]
    public Vector2? West;
}
