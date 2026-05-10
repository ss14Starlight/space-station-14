using Content.Shared.Random.Rules;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Random.Rules;

public sealed partial class OnMapTagRule : RulesRule
{
    [DataField(required: true)]
    public ProtoId<TagPrototype> mapTag;

    public override bool Check(EntityManager entManager, EntityUid uid)
    {
        if (!entManager.TryGetComponent(uid, out TransformComponent? xform) ||
            xform.MapUid == null)
        {
            return false;
        }

        var tagSystem = entManager.System<TagSystem>();

        if (tagSystem.HasTag(xform.MapUid.Value, mapTag))
            return !Inverted;

        return Inverted;
    }
}
