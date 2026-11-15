using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Traits.Effects;

/// <summary>
/// Effect that spawns an item and attempts to implant it.
/// </summary>
public sealed partial class SpawnItemAsImplantEffect : BaseTraitEffect
{
    /// <summary>
    /// The entity prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item = string.Empty;

    /// <summary>
    /// The slot to spawn it into.
    /// </summary>
    [DataField(required: true)]
    public string Slot = string.Empty;

    public override void Apply(TraitEffectContext ctx)
    {
        // This effect needs to be applied server-side where we have access to
        // SharedBodySystem. The actual spawning logic is handled by the server TraitSystem.
        // This class just holds the data.
    }
}
