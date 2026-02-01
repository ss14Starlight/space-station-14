using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Potions;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeFireproofPotionComponent : Component
{
    /// <summary>
    /// How many uses of this potion remain.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int RemainingUses = 3;

    /// <summary>
    /// The specific damage set applied to the entity
    /// </summary>
    [DataField("fireproofDamageSet", required: true), AutoNetworkedField]
    public ProtoId<DamageModifierSetPrototype> FireproofDamageSet = default!;
}