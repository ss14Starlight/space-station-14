namespace Content.Shared._Starlight.Doors.Components;

/// <summary>
/// Explicitly enables split visuals for wall mounts on a DoorComponent whose
/// two panels slide in opposite directions along the door's local X axis.
/// </summary>
[RegisterComponent]
public sealed partial class DoorMountSplitComponent : Component
{
    /// <summary>
    /// Distance each visual half travels, in tile units. The default matches
    /// the secret door's panel animation.
    /// </summary>
    [DataField]
    public float SlideDistance = 0.45f;
}
