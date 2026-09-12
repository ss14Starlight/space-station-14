using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.WallStains.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WallStainComponent : Component
{
    [DataField, AutoNetworkedField]
    public string SolutionName = "stain";

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxStainVolume = FixedPoint2.New(5);

    [DataField, AutoNetworkedField]
    public Color Color { get; set; } = Color.White;

    [DataField, AutoNetworkedField]
    public string StainState { get; set; } = "splatter";

    // Tracks which face of the wall this stain is applied to
    [DataField, AutoNetworkedField]
    public Vector2i Direction { get; set; } = Vector2i.Zero;
}
