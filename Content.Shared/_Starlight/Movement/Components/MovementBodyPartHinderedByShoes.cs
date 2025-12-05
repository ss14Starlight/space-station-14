using Robust.Shared.GameStates;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Starlight.Movement.Components;

/// <summary>
/// A component that applies a percentange-based modifier to movement speed while this mob is wearing shoes.
/// Typical use-case is to apply a movement penalty (slow) to a mob that puts on shoes.
/// 
/// This component is applied to leg BodyParts (e.g. natural limbs like LeftLegFelionoid).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MovementBodyPartHinderedByShoesComponent : Component
{
    [DataField]
    public float HinderModifier = 0.0f;
}