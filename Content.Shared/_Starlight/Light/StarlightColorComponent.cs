using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Light;

/// <summary>
/// Sets the color of the starlight that open space throws onto a map, put on the map entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StarlightColorComponent : Component
{
    /// <summary>
    /// Color of the starlight in sRGB, the same way colours are written in prototypes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#9db8ff");
}
