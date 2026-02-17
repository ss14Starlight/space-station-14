using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Genetics;

/*
 * The difference between a PassiveSlimeTrait and a PassiveSlimeExtractTrait is what they act on.
 * PassiveSlimeTrait acts on the slime itself.
 * PassiveSlimeExtractTrait acts on the extract from the slime after it gets processed.
 */

/// <summary>
/// A trait that acts on a slime extract that gets constantly activated.
/// USE THIS RARELY: Constant activation is expensive. Try making the cooldown high.
/// </summary>
[Prototype]
public sealed partial class PassiveSlimeExtractTraitPrototype : AbstractXenobiologyTraitPrototype
{
    /// <summary>
    /// The entity effect that gets activated.
    /// </summary>
    [DataField(required: true)]
    public ScaledEntityEffect EntityEffect = default!;
    
    /// <summary>
    /// How long after an activation to wait until the next activation.
    /// DO NOT SET THIS TO 0 UNLESS YOU ARE PREPARED FOR THE SLOWDOWN.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Cooldown = default!;
}