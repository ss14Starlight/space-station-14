using System;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Railroading.Components.Reward;

[RegisterComponent]
public sealed partial class RailroadDonationRewardComponent : Component
{
    [DataField]
    public MinMax Amount;
}
