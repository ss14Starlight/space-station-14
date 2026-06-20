using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server.Damage.Components;

[RegisterComponent]
public sealed partial class IgniteOnHeatDamageComponent : Component
{
    [DataField("fireStacks")]
    public float FireStacks = 1f;
    #region Starlight
    // Max fire stacks that can be applied by this effect, stop adding fire stacks after this.
    [DataField("maxFireStacks")]
    public float MaxFireStacks = 10f;
    #endregion
    // The minimum amount of damage taken to apply fire stacks
    [DataField("threshold")]
    public FixedPoint2 Threshold = 15;
}
