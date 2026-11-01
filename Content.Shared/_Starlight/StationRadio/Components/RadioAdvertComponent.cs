using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent]
public sealed partial class RadioAdvertComponent : Component
{
    [DataField]
    public int RewardPerListener = 25;

    [DataField]
    public int MaxReward = 500;

    /// <summary>
    /// Department account the money goes to.
    /// </summary>
    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Service";
}
