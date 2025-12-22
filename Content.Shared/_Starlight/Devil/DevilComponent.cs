using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Devil;
using Content.Shared.Dataset;
using Content.Shared.Damage;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DevilComponent : Component
{
    [DataField]
    public List<ProtoId<EntityPrototype>> BaseActions = new()
    {
        "ActionSummonDemonicContract"
    };

    
    [DataField]
    public List<ProtoId<DamnationPrototype>> AvailableDamnations = new()
    {
        "Soul",
        "Pacifism",
        "Blindness",
        "SpaceImmunity",
        "Credits"
    };

    // todo make actual devil names
    public List<ProtoId<LocalizedDatasetPrototype>> NameSegments = new()
    {
        "NamesDragon",
        "NamesDragonTitle"
    };

    public LocId NameFormat = "name-format-dragon";

    [AutoNetworkedField, ViewVariables]
    public string TrueName = "Hellish McEvil";

    /// <summary>
    /// Is the devil currently being banished?
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public bool BeingBanished = false;

    /// <summary>
    /// Time of the last banish shift starting
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public TimeSpan LastBanishModeActivate = TimeSpan.Zero;

    /// <summary>
    /// How long should devil be stuck being banished?
    /// </summary>
    [DataField]
    public TimeSpan BanishModeLength = TimeSpan.FromMinutes(2);

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
            { "Heat", 7 },
        }
    };

    /// <summary>
    /// How much stamina damage to take per banish
    /// </summary>
    [DataField]
    public float BanishDamageStamina = 40.0f;

    /// <summary>
    /// Damnation that increments the evil-ness of the devil
    /// </summary>
    [DataField]
    public ProtoId<DamnationPrototype> SoulDamnation = "Soul";

    /// <summary>
    /// list of people who have been evil'd
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public List<EntityUid> DamnedSouls = new();
}