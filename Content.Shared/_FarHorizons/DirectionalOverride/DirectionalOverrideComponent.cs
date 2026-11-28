using System.Numerics;

namespace Content.Shared._FarHorizons.DirectionalOverride;

[RegisterComponent]
public sealed partial class DirectionalOverrideComponent : Component
{
    [DataField]
    public Dictionary<Direction, Vector2> Offsets = new();

    [DataField]
    public Dictionary<Direction, DrawDepth.DrawDepth> DrawDepths = new();

    [ViewVariables]
    public TimeSpan NextUpdate;
}
