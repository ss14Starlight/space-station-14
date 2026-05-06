using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Paper;

[RegisterComponent]
public sealed partial class AdjustChargesPopBasedComponent : Component
{
    [DataField]
    public FixedPoint2 Percent = 0.65;
}
