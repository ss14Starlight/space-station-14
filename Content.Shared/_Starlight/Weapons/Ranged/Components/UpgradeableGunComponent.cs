// ReSharper disable CheckNamespace
using Robust.Shared.GameStates;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Upgrades.Components;

public sealed partial class UpgradeableGunComponent
{
    [DataField]
    public ProtoId<ToolQualityPrototype> UpgradeRemovalMethod = "Screwing";

    [DataField]
    public float UpgradeRemovalDelay = 10f;
}
