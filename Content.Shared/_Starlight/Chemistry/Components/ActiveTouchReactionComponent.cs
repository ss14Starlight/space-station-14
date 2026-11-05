using Content.Shared.FixedPoint;

namespace Content.Shared._Starlight.Chemistry.Components;

[RegisterComponent]
public sealed partial class ActiveTouchReactionComponent: Component
{
    [DataField("amountPerReaction")]
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 AmountPerReaction { get; set; } = 10;
}