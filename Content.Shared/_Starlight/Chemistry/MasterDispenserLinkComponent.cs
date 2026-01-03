using Robust.Shared.GameStates;

namespace Content.Shared.Starlight.Chemistry;

/// <summary>
/// Component on a reagent dispenser that tracks its linked ChemMaster.
/// Similar to OreSiloClientComponent but for reagent dispenser -> chemmaster linking.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MasterDispenserLinkComponent : Component
{
    /// <summary>
    /// The ChemMaster that this dispenser is linked to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedChemMaster;

    /// <summary>
    /// Whether the dispenser is currently in "transfer to ChemMaster" mode.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TransferToChemMaster;

    /// <summary>
    /// The maximum distance to search for ChemMasters to link to.
    /// </summary>
    [DataField]
    public float Range = 10f;
}
