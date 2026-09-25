using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared._Starlight.Combat.Ranged.Pierce;

namespace Content.Shared._Starlight.Weapons.Ranged.Ammo;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(GunToggleableAmmoSystem))]
public sealed partial class GunToggleableAmmoComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public List<GunToggleableAmmoSetting> Settings = new();

    [DataField, AutoNetworkedField]
    public int Setting;

    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "MedTakActionToggleAmmo";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/_Starlight/Weapons/Guns/Cock/gun_boomslang_lever.ogg");

    [DataField, AutoNetworkedField]
    public string Message = "toggleable-ammo-firing";
}

[DataRecord]
[Serializable, NetSerializable]
public readonly partial record struct GunToggleableAmmoSetting(DamageSpecifier Damage, float ArmorPiercing, float RicochetChance, float PierceChance, float PierceDeviation, PierceLevel PierceLevel, LocId Name, SpriteSpecifier.Rsi Icon);
