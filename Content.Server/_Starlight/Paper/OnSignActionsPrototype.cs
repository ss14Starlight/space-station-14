
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper;

[Prototype]
public sealed partial class OnSignActionsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("actions", required: true)]
    public List<OnSignAction> Actions = new();
}