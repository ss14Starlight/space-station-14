using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Daiquiri.Weapons.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KatanaStanceComponent : Component
{
    [DataField]
    public float AttackWalkModifier = 1.1f;

    [DataField]
    public float AttackSprintModifier = 1.1f;

    [DataField]
    public float DefenseWalkModifier = 0.8f;

    [DataField]
    public float DefenseSprintModifier = 0.8f;

    [DataField]
    public float AttackDamageMultiplier = 1f;

    [DataField]
    public float DefenseDamageMultiplier = 1f;

    [DataField]
    public bool CanAttackInDefense;

    [DataField]
    public float DefenseBlockChance = 1f;

    [DataField]
    public float AttackBlockChance = 0.2f;

    [DataField]
    public float DisarmBlockChance = 1f;

    [DataField]
    public float DefenseReflectChance = 0.15f;

    [DataField]
    public float AttackReflectChance = 0.05f;

    [DataField]
    public ReflectType Reflects = ReflectType.Energy | ReflectType.NonEnergy;

    [DataField, AutoNetworkedField]
    public bool FlipSpriteInDefense = true;

    [DataField]
    public List<string> FlipSpriteLayers = new() { "icon" };

    [DataField]
    public string BlockedPopup = "katana-blocked";

    [DataField]
    public string AttackBlockedPopup = "katana-attack-blocked";

    [DataField]
    public SoundSpecifier BlockSound = new SoundPathSpecifier("/Audio/Weapons/block_metal1.ogg");

    [DataField]
    public SoundSpecifier ReflectSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Hits/laser_sear_wall.ogg");
}