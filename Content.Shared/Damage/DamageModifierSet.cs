using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Damage
{
    /// <summary>
    ///     A set of coefficients or flat modifiers to damage types. Can be applied to <see cref="DamageSpecifier"/> using <see
    ///     cref="DamageSpecifier.ApplyModifierSet(DamageSpecifier, DamageModifierSet)"/>. This can be done several times as the
    ///     <see cref="DamageSpecifier"/> is passed to it's final target. By default the receiving <see cref="DamageableComponent"/>, will
    ///     also apply it's own <see cref="DamageModifierSet"/>.
    /// </summary>
    /// <remarks>
    /// The modifier will only ever be applied to damage that is being dealt. Healing is unmodified.
    /// </remarks>
    [DataDefinition]
    [Serializable, NetSerializable]
    [Virtual]
    public partial class DamageModifierSet
    {
        [DataField("coefficients", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<float, DamageTypePrototype>))]
        public Dictionary<string, float> Coefficients = new();

        [DataField("flatReductions", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<float, DamageTypePrototype>))]
        public Dictionary<string, float> FlatReduction = new();

        // BEGIN STARLIGHT
        public DamageModifierSet(DamageModifierSet dms)
        {
            // copying constructor for duplicating damage modifier sets
            // some magic status effects can have variable or decaying damage modifiers
            // therefore, we need a cheap and idiomatic way to create copies of DamageModifierSets that can be safely be modified
            // (may also prevent memory leaks if the prototype that creates DamageModifierSets is generated at runtime)
            Coefficients = new Dictionary<string, float>(dms.Coefficients);
            FlatReduction = new Dictionary<string, float>(dms.FlatReduction);
        }

        // END STARLIGHT
    }
}
