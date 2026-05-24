using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._TP.Chemistry.UI;

/// <summary>
///     Created by Cookie for multiple servers.
///     Initializes a <see cref="ModernChemMasterWindow"/> and updates it when new server messages are received.
/// </summary>
[UsedImplicitly]
public sealed class ModernChemMasterBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ModernChemMasterWindow? _window;

    /// <summary>
    ///     Called each time a chem master UI instance is opened.
    ///     Generates the window and fills it with relevant info. Sets the actions for static buttons.
    /// </summary>
    protected override void Open()
    {
        base.Open();

        // Set-up the window layout/elements
        _window = this.CreateWindow<ModernChemMasterWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        // Set-up the static button actions.
        _window.InputEjectButton.OnPressed += _ => SendMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));
        _window.OutputEjectButton.OnPressed += _ => SendMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.OutputSlotName));
        _window.BufferTransferButton.OnPressed += _ => SendMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Transfer));
        _window.BufferDiscardButton.OnPressed += _ => SendMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Discard));
        _window.CreatePillButton.OnPressed += _ => SendMessage(
            new ChemMasterCreatePillsMessage(
                (uint) _window.PillDosage.Value, // Starlight
                (uint) _window.PillNumber.Value, // Starlight
                _window.LabelLine, // Starlight
                _window.ContainerLabelLine)); // Starlight
        _window.CreateBottleButton.OnPressed += _ => SendMessage(
            new ChemMasterOutputToBottleMessage(
                (uint) _window.BottleDosage.Value, _window.LabelLine));
        _window.BufferSortButton.OnPressed += _ => SendMessage(
            new ChemMasterSortingTypeCycleMessage());
        _window.BufferSortButtonClassic.OnPressed += _ => SendMessage(
            new ChemMasterSortingTypeCycleMessage());
        _window.OutputBufferDraw.OnPressed += _ => SendMessage(
            new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.Internal));
        _window.OutputBeakerDraw.OnPressed += _ => SendMessage(
            new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.External));

        for (uint i = 0; i < _window.PillTypeButtons.Length; i++)
        {
            var pillType = i;
            _window.PillTypeButtons[i].OnPressed += _ => SendMessage(new ChemMasterSetPillTypeMessage(pillType));
        }

        _window.OnReagentButtonPressed += (_, button) => SendMessage(new ChemMasterReagentAmountButtonMessage(button.Id, button.Amount, button.IsBuffer));

        _window.OnAmountSelected += amount => SendMessage(new ChemMasterSetTransferAmountMessage(amount));
    }

    /// <summary>
    /// Update the ui each time new state data is sent from the server.
    /// </summary>
    /// <param name="state">
    /// Data of the <see cref="SharedReagentDispenser"/> that this ui represents.
    /// Sent from the server.
    /// </param>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var castState = (ChemMasterBoundUserInterfaceState) state;

        _window?.SetSelectedAmount(castState.TransferAmount);
        _window?.UpdateState(castState); // Update window state
    }
}
