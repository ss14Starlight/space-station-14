namespace Content.Shared._Starlight.StationGridMemory;

[RegisterComponent]
public sealed partial class StationGridMemoryComponent : Component
{
    /// <summary>
    /// The last station this entity was on.
    /// </summary>
    [ViewVariables] public EntityUid LastStation;
}