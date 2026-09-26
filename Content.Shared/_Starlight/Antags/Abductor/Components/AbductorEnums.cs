using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Antags.Abductor.Components;

[Serializable, NetSerializable]
public enum AbductorExperimentatorVisuals : byte
{
    Full
}
[Serializable, NetSerializable]
public enum AbductorOrganType : byte
{
    None,
    Health,
    NitrousOxide,
    Gravity,
    Egg,
    Spider,
    Vent
}
[Serializable, NetSerializable]
public enum AbductorArmorModeType : byte
{
    Combat,
    Stealth
}

[Serializable, NetSerializable]
public enum AbductorConsoleUIKey
{
    Key
}
