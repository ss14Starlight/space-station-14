using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.SubFloor;

public sealed partial class SubFloorHideComponent
{
    /// <summary>
    ///     Whether this entity can be anchored and unanchored while a floor tile covers it.
    /// </summary>
    [DataField]
    public bool AllowAnchoringUnderCover { get; set; }

    /// <summary>
    ///     This determines what subfloor layers this entity is visible on.
    /// </summary>
    [DataField(customTypeSerializer:typeof(FlagSerializer<VisibilityMask>))]
    public int SubfloorLayer { get; set; }
}

public sealed class VisibilityMask;
