// ReSharper disable CheckNamespace
using Robust.Shared.GameStates;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Upgrades.Components;

public sealed partial class UpgradeableGunComponent
{
///<summary>
///default removal method
///</summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> UpgradeRemovalMethod = "Screwing";

///<summary>
///Time it takes to remove a mod in seconds
///</summary>
    [DataField]
    public float UpgradeRemovalDelay = 10f;
}
