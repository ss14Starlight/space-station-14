// ReSharper disable CheckNamespace
using Robust.Shared.GameStates;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Upgrades.Components;

public sealed partial class UpgradeableGunComponent
{
//default removal method
    [DataField]
    public ProtoId<ToolQualityPrototype> UpgradeRemovalMethod = "Screwing";

//Time it takes to remove a mod in seconds
    [DataField]
    public float UpgradeRemovalDelay = 10f;
}
