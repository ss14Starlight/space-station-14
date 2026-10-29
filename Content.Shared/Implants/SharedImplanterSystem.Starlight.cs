using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Implants;

public abstract partial class SharedImplanterSystem
{
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> FragileImplantTag = "FragileImplant";

    private bool IsFragileImplant(EntityUid implant)
        => _tags.HasTag(implant, FragileImplantTag);
}
