using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Dataset;
using Content.Shared.Damage;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DevilComponent : Component
{
    [DataField]
    public List<ProtoId<EntityPrototype>> BaseActions = new()
    {
        "ActionSummonDemonicContract",
        "ActionSummonDevilPen",
        "ActionDamnationsMenu",
        "ActionDevilRejuvenate"
    };

    /// <summary>
    /// What damnations can the devil use in their contracts?
    /// </summary>
    [DataField]
    public List<ProtoId<DamnationPrototype>> AvailableDamnations = new()
    {
        "Soul",
        "Pacifism",
        "Blindness",
        "SpaceImmunity",
        "Credits",
        "AllSeeing",
        "Magic",
        "Purpose",
        "Health",
        "Time",
        "Organ",
        "Power",
        "Gun",
        "Electricity",
        "Noslip",
        "Mute"
    };

    /// <summary>
    /// Damnation that increments the evil-ness of the devil
    /// </summary>
    [DataField]
    public ProtoId<DamnationPrototype> SoulDamnation = "Soul";

    /// <summary>
    /// list of people who have been evil'd
    /// </summary>
    public List<EntityUid> DamnedSouls = new();

    // todo make actual devil names
    public List<ProtoId<LocalizedDatasetPrototype>> NameSegments = new()
    {
        "NamesDevil",
        "NamesDevilTitle"
    };

    public LocId NameFormat = "name-format-devil";

    [AutoNetworkedField, ViewVariables]
    public string TrueName = "Hellish McEvil";

    [DataField]
    public EntProtoId InfernalContractPrototype = "InfernalContract";

    [DataField]
    public SoundPathSpecifier ContractSummonSound = new("/Audio/Effects/thudswoosh.ogg");

    [DataField, AutoNetworkedField]
    public DevilChangeCriteria RedEyesAppearance = new(1);

    [DataField, AutoNetworkedField]
    public DevilChangeCriteria RedAuraAppearance = new (3);

    [DataField, AutoNetworkedField]
    public DevilChangeCriteria OminousHum = new (4);

    [DataField, AutoNetworkedField]
    public DevilChangeCriteria InfernalJauntAction = new (5);

    [DataField, AutoNetworkedField]
    public DevilChangeCriteria EvilHaloAppearance = new (6);

    [DataField, AutoNetworkedField]
    public DevilChangeCriteria BidentAction = new (7);

    [DataField]
    public EntProtoId SummonBidentActionProto = "ActionSummonBident";

    [DataField]
    public EntProtoId InfernalJauntActionProto = "ActionInfernalJaunt";

    /// <summary>
    /// How long is the damage cooldown per person?
    /// </summary>
    [DataField]
    public TimeSpan BanishCooldown = TimeSpan.FromSeconds(3); // 3s cooldown per person

    /// <summary>
    /// List of the last times people banished the devil
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public Dictionary<EntityUid, TimeSpan> LastBanishedList = new();

    /// <summary>
    /// How much damage to take per banish
    /// </summary>
    [DataField]
    public DamageSpecifier BanishDamage = new()
    {
        DamageDict = new()
        {
            { "Cellular", 6 },
        }
    };

    /// <summary>
    /// How much stamina damage to take per banish
    /// </summary>
    [DataField]
    public float BanishDamageStamina = 40.0f;

    /// <summary>
    /// How much damage to take from bible kill
    /// </summary>
    [DataField]
    public DamageSpecifier BibleBanishDamage = new()
    {
        DamageDict = new()
        {
            { "Cellular", 777 },
        }
    };
}

[Serializable, NetSerializable]
public record struct DevilChangeCriteria(int AtSouls, bool Completed = false);
