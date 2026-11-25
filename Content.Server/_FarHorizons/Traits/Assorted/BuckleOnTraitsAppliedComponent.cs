using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.Traits.Assorted;

/// <summary>
/// Upon applying traits buckles the attached entity to a newly spawned prototype.
/// </summary>
[RegisterComponent, Access(typeof(BuckleOnTraitsAppliedSystem))]
public sealed partial class BuckleOnTraitsAppliedComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(required: true)]
    public EntProtoId Prototype;
}
