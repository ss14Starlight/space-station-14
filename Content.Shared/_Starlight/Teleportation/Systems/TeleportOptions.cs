namespace Content.Shared._Starlight.Teleportation.Systems;

/// <summary>
/// Options for <see cref="SharedTeleportSystem.Teleport"/>. Can be adjusted at the caller.
/// </summary>
public sealed class TeleportOptions
{
    /// <summary>
    /// Whether anything the entity teleporting is pulling comes along instead of being left behind.
    /// </summary>
    public bool CarryPulled = true;

    /// <summary>
    /// Whether whoever is pulling the teleported entity comes along instead of being left behind.
    /// </summary>
    public bool CarryIncomingPulls = true;

    /// <summary>
    /// Heaviest entity that can come along, in kilograms. Heavier targets stay behind and the pull is broken.
    /// Player characters cap out around 115.
    /// </summary>
    public float CarryMassLimit = 150f;
}
