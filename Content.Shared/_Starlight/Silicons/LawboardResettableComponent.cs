using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Silicons;

[RegisterComponent, NetworkedComponent]
public sealed partial class LawboardResettableComponent : Component
{
    [DataField]
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(10.0f);

    [DataField(required: true)]
    public ProtoId<SiliconLawsetPrototype> DefaultLaws { get; set; }
}
