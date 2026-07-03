using Content.Shared.EntityEffects;

namespace Content.Shared._Starlight.EntityEffects.Effects;

/// <summary>
/// Spawns a small text popup on an entity.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class Popup: EntityEffectBase<Popup>
{
    /// <summary>
    /// Text that is popped up.
    /// </summary>
    [DataField(required: true)]
    public string Text = default!;
}
