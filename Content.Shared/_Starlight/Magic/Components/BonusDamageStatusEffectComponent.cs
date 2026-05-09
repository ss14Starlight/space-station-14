using Robust.Shared.GameStates;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Factory for adding <see cref="BonusDamageComponent"/>s to mobs. These are used to add flat damage amounts to attacks.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedBonusDamageSystem))]
public sealed partial class BonusDamageStatusEffectComponent : Component
{
    [DataField("damageTypes", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<float, DamageTypePrototype>), required: true)]
    public Dictionary<string, float> DamageTypes = new();

    [DataField]
    public bool AffectsUnarmed = false;

    [DataField]
    public bool AffectsMeleeWeapons = false;

    [DataField]
    public bool AffectsRangedWeapons = false;

    [DataField]
    public bool OverwriteOnRefresh = false;
}
