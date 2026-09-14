namespace Content.Shared.SubFloor;

public sealed partial class SubFloorHideComponent
{
    /// <summary>
    ///     Whether this entity can be anchored and unanchored while a floor tile covers it.
    /// </summary>
    [DataField]
    public bool AllowAnchoringUnderCover { get; set; }
}
