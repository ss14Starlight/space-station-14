using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Photography;

/// <summary>
/// PDA cartridge that grants snapshot capture to the loader PDA while installed.
/// </summary>
[RegisterComponent]
public sealed partial class PhotoCartridgeComponent : Component
{
    /// <summary>
    /// Components added to the loader by <see cref="PhotoCartridgeFilmSystem"/> while installed.
    /// </summary>
    [DataField]
    public ComponentRegistry GrantedComponents = new();
}
