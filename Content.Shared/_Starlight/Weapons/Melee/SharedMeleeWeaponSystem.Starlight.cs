using Content.Shared.Tag;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    [Dependency] private TagSystem _tag = default!;
    private static readonly ProtoId<TagPrototype> WideSwingPassThroughDomeTag = "WideSwingPassThroughDome";

    private bool IsWideSwingPassThroughDome(EntityUid entity)
        => _tag.HasTag(entity, WideSwingPassThroughDomeTag);
}
