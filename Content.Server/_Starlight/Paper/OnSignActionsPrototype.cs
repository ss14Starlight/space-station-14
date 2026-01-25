
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper;

[Prototype]
public sealed partial class OnSignActionsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("actions", required: true)]
    public List<OnSignAction> Actions = new();
}
