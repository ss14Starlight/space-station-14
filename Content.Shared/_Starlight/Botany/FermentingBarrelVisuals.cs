using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Botany;

/// <summary>
/// Appearance data for the fermenting barrel: whether its lid is off.
/// </summary>
[Serializable, NetSerializable]
public enum FermentingBarrelVisuals : byte
{
    Open,
}
