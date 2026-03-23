using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.VisualPickupable;

[RegisterComponent]
public sealed partial class VisualPickupableComponent : Component
{
    [ViewVariables] public EntityUid? ClonedVisuals = null;

    [DataField] public Vector2 OffsetFront = new Vector2(0, -0.1f);
    [DataField] public Vector2 OffsetBack = new Vector2(0, 0.1f);
    [DataField] public float AngleDegrees = 90;

}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PickupableVisualsComponent : Component
{
    [ViewVariables, AutoNetworkedField] public EntityUid? Source = null;
}