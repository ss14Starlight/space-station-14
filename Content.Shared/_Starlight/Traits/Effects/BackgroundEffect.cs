using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Traits.Effects;

/// <summary>
/// Effect that add/replace a background to the player entity.
/// </summary>
public sealed partial class BackgroundEffect : BaseTraitEffect
{
    /// <summary>
    /// The background of the entity.
    /// </summary>
    [DataField(required: true)]
    public string Background;

    public override void Apply(TraitEffectContext ctx)
    {
        if (!ctx.EntMan.EntitySysManager.TryGetEntitySystem(out TagSystem? tagsystem))
            return;

        var tag = new ProtoId<TagPrototype>(Background + "TraitBackground");
        tagsystem.TryAddTag(ctx.Player, tag);
    }
}