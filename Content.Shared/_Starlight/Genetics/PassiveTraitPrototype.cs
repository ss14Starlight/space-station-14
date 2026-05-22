using Content.Shared._Starlight.Xenobiology;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Genetics;

/// <summary>
/// A trait that acts on a slime that gets constantly activated.
/// USE THIS RARELY: Constant activation is expensive. Try making the cooldown high.
/// </summary>
[Prototype]
public sealed partial class PassiveTraitPrototype : AbstractTraitPrototype
{
    /// <summary>
    /// The entity effect that gets activated.
    /// </summary>
    [ViewVariables]
    [DataField(required: true)]
    public ScaledEntityEffect EntityEffect = default!;

    /// <summary>
    /// How long after an activation to wait until the next activation.
    /// DO NOT SET THIS TO 0 UNLESS YOU ARE PREPARED FOR THE SLOWDOWN.
    /// </summary>
    [ViewVariables]
    [DataField(required: true)]
    public TimeSpan Cooldown = default!;
}
