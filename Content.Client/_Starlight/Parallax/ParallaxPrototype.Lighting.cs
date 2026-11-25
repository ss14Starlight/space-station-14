// ReSharper disable CheckNamespace
namespace Content.Client.Parallax.Data;

public sealed partial class ParallaxPrototype
{
    /// <summary>
    /// Color of the starlight that open space throws onto maps with this parallax.
    /// Unset keeps the default pale blue.
    /// </summary>
    [DataField]
    public Color? StarlightColor { get; private set; }
}
