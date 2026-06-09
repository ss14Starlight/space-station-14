using Content.Shared.Random.Rules;

namespace Content.Shared._Starlight.Random.Rules;

/// <summary>
/// Returns true if on grid
/// </summary>
public sealed partial class OnGridRule : RulesRule
{
    public override bool Check(EntityManager entManager, EntityUid uid)
        => !entManager.TryGetComponent(uid, out TransformComponent? xform) || xform.GridUid == null ? Inverted : !Inverted;
}
