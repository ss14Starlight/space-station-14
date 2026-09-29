using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Honeypot.Components;

/// <summary>
/// Marks an entity as needing to be replaced with the specified prototype, in addition to the extra component overrides, on map init.
/// </summary>
[RegisterComponent]
public sealed partial class ReplaceOnInitComponent : Component
{
    /// <summary>
    /// The real entity to spawn in this entity's place. It receives <see cref="AdminNotifyOnDamageComponent"/>.
    /// </summary>
    [DataField(required: true)] public EntProtoId Proto;

    /// <summary>
    /// Extra component overrides applied to the spawned replacement.
    /// </summary>
    [DataField] public ComponentRegistry Overrides = new();
}
