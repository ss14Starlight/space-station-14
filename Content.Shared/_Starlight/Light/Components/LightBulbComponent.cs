using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Light.Components;

public sealed partial class LightBulbComponent
{
    /// <summary>
    /// Is this light dimmed by alert levels? Bulbs that aren't stay on as emergency lighting.
    /// Night lighting dims every bulb regardless.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Dimmable = true;
}
