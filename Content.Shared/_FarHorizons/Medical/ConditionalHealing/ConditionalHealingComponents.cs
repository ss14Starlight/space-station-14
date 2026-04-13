using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Medical.Healing;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Medical.ConditionalHealing;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class ConditionalHealingData
{
    [DataField]
    public DamageSpecifier Damage = default!;
    [DataField]
    public float BloodlossModifier = 0.0f;
    [DataField]
    public float ModifyBloodLevel = 0.0f;
    [DataField]
    public List<ProtoId<DamageContainerPrototype>>? DamageContainers;
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3f);
    [DataField]
    public float SelfHealPenaltyMultiplier = 3f;
    [DataField]
    public SoundSpecifier? HealingBeginSound = null;
    [DataField]
    public SoundSpecifier? HealingEndSound = null;
    [DataField]
    public bool SolutionDrain = false;
    [DataField]
    public List<ReagentQuantity> ReagentsToDrain = [];

    [DataField]
    public int AdjustEyeDamage = 0;

    public HealingComponent MakeComponent() =>
        new()
        {
            Damage = Damage,
            BloodlossModifier = BloodlossModifier,
            ModifyBloodLevel = ModifyBloodLevel,
            DamageContainers = DamageContainers,
            Delay = Delay,
            SelfHealPenaltyMultiplier = SelfHealPenaltyMultiplier,
            HealingBeginSound = HealingBeginSound,
            HealingEndSound = HealingEndSound,
            SolutionDrain = SolutionDrain,
            ReagentsToDrain = ReagentsToDrain,
            AdjustEyeDamage = AdjustEyeDamage
        };
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class ConditionalHealingDefition
{
    [DataField]
    public HashSet<ProtoId<TagPrototype>> AllowedTags = [];
    [DataField]
    public ConditionalHealingData Healing = default!;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConditionalHealingComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public List<ConditionalHealingDefition> HealingDefinitions = [];

}
