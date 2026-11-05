namespace Content.Shared._Funkystation.Footprints;

public sealed partial class FootprintOwnerComponent : Component
{
    /// <summary>
    /// Which print this entity leaves while standing.
    /// </summary>
    [DataField]
    public FootprintVisualState PrintState = FootprintVisualState.Foot;
}
