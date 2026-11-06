namespace Content.Shared._Funkystation.Footprints;

public sealed partial class FootprintOwnerComponent : Component
{
    /// <summary>
    /// Which print this entity leaves while standing and unshod. Anything in the shoes slot
    /// overrides this with the generic boot print.
    /// </summary>
    [DataField]
    public FootprintVisualState PrintState = FootprintVisualState.Barefoot;
}
