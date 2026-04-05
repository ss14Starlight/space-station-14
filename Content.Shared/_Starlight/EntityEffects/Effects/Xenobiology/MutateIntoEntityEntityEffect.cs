using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.EntityEffects.Effects.Xenobiology;

public sealed partial class MutateIntoEntity : EntityEffectBase<MutateIntoEntity>
{
    /// <summary>
    /// Prototype of the entity we're mutating into.
    /// </summary>
    [DataField (required: true)]
    public EntProtoId Entity;
}
