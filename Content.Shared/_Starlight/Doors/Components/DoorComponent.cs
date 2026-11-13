namespace Content.Shared.Doors.Components;

public sealed partial class DoorComponent
{
    /// <summary>
    /// If the door crushed someone should they be pushed out of the way.
    /// </summary>
    [DataField] public bool CrushPush;
    /// <summary>
    /// If the door crushed someone should it keep closed.
    /// </summary>
    [DataField] public bool KeepClosed;

}
