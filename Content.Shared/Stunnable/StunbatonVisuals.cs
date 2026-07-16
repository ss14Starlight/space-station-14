using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Stunbaton;

/// <summary>
/// Appearance keys for the turbine.
/// </summary>
[Serializable, NetSerializable]
public enum StunbatonVisuals
{
    Stunbaton_on,
    Stunbaton_off,
    Stunbaton_nocell,
}

/// <summary>
/// Visual sprite layers for the turbine.
/// </summary>
[Serializable, NetSerializable]
public enum StunbatonVisualLayers
{
    Stunbaton_on,
    Stunbaton_off,
    Stunbaton_nocell,
}
