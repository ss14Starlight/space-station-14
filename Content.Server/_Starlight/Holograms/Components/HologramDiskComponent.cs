using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms;

/// <summary>
///     Marks this entity as storing a hologram's data in it, for use in a <see cref="HologramServerComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class HologramDiskComponent : Component
{
    /// <summary>
    ///     The mind stored in this Holodisk.
    /// </summary>
    [ViewVariables]
    public EntityUid? HoloMind = null;

    /// <summary>
    ///     The prototype ID of a hologram mob to spawn when this disk is used.
    ///     Used for NPC holograms like the holo corgi.
    /// </summary>
    [DataField]
    public EntProtoId? HologramPrototype;

    /// <summary>
    ///     Temporarily stores the user who attempted to save someone to the disk,
    ///     for showing feedback messages after consent is given.
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingUser = null;

    /// <summary>
    ///     Temporarily stores the mind being saved to disk while awaiting consent.
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingMind = null;
}
