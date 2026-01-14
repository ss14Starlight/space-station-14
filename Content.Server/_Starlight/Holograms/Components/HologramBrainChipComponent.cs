namespace Content.Server._Starlight.Holograms;

/// <summary>
///     Stores a mind for holographic projection.
///     Output by body scanners, can only speak when slotted in a powered blade server.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBrainChipComponent : Component
{
    /// <summary>
    ///     The mind stored in this brain chip.
    ///     Forcibly transferred from scanned person.
    /// </summary>
    [ViewVariables]
    public EntityUid? HoloMind = null;

    /// <summary>
    ///     Whether this chip is currently in a powered hologram blade server.
    ///     Only chips in powered servers can allow the mind to speak.
    /// </summary>
    [ViewVariables]
    public bool IsPowered = false;
}
