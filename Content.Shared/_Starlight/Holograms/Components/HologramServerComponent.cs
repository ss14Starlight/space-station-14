namespace Content.Shared._Starlight.Holograms;

/// <summary>
///     Marks an entity as being a hologram projection server.
/// </summary>
[RegisterComponent]
public sealed partial class HologramServerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? LinkedHologram;
}
