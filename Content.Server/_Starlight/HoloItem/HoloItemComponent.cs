using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server._Starlight.HoloItem;

[RegisterComponent]
public sealed partial class HoloItemComponent : Component
{
    [DataField]
    public ProtoId<EntityPrototype>? ItemPrototype;

    [DataField]
    public bool UseOnTarget = true;

    [DataField]
    public float ChargeUse = 50f;

    [DataField]
    public List<string> RequiredComponents { get; set; } = new();
}