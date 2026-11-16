using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Server.Chemistry.Components;

/// <summary>
/// An industrial grade chemical manipulator with pill and bottle production included.
/// <seealso cref="ChemMasterSystem"/>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedChemMasterSystem))]
public sealed partial class ChemMasterComponent : Component
{
    [DataField, AutoNetworkedField]
    public uint PillType = 0;

    [DataField, AutoNetworkedField]
    public ChemMasterMode Mode = ChemMasterMode.Transfer;

    [DataField, AutoNetworkedField]
    public ChemMasterSortingType SortingType = ChemMasterSortingType.None;

    [DataField(required: true)]
    public uint PillDosageLimit;

    [DataField(required: true)]
    public uint PatchDosageLimit;

    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    /// <summary>
    /// Which source the chem master should draw from when making pills/bottles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ChemMasterDrawSource DrawSource = ChemMasterDrawSource.External;

    [DataField, AutoNetworkedField]
    public ChemMasterReagentAmount TransferAmount = ChemMasterReagentAmount.U5;
}
