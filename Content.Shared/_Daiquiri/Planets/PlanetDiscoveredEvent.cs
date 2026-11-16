using Robust.Shared.Serialization;

namespace Content.Shared._Daiquiri.Planets;

/// <summary>
/// Sent to a player when they step onto a planet so their client can show the discovery banner.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlanetDiscoveredEvent : EntityEventArgs
{
    public string Name = string.Empty;

    public Color Accent = Color.White;
}
