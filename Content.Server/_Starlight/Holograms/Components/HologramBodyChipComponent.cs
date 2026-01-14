using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Holograms;

/// <summary>
///     Stores physical appearance data (body type, clothing, etc.) for holographic projection.
///     Output by body scanners.
/// </summary>
[RegisterComponent]
public sealed partial class HologramBodyChipComponent : Component
{
    /// <summary>
    ///     The prototype ID of a hologram mob appearance to use.
    ///     Contains body structure, appearance, and equipment data.
    /// </summary>
    [DataField]
    public EntProtoId? HologramPrototype;

    /// <summary>
    ///     The name to display for this hologram body.
    /// </summary>
    [DataField]
    public string? HologramName;
}
