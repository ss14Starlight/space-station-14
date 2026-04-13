using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Xenobiology.Potions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeBluespaceRadioPotionComponent : Component
{
    /// <summary>
    /// The set of channels the recipient of the potion will subscribe to.
    /// </summary>
    [DataField("channels", required: true)]
    public HashSet<ProtoId<RadioChannelPrototype>> Channels = default!;
}
