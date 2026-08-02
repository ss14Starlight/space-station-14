namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// A visible fungal reservoir carrying the runtime strain that created it.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenSporePatchComponent : Component
{
    [DataField]
    public int Strain;
}
