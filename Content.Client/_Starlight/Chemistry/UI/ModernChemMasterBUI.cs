using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Chemistry.UI;

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
        _window.SetOwner(Owner);
        _window.SetChemMasterEntity(EntMan.GetNetEntity(Owner));

        // Set-up the static button actions.
        _window.InputEjectButton.OnPressed += _ => SendPredictedMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));
        _window.InputEjectButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));
        _window.OutputEjectButton.OnPressed += _ => SendPredictedMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.OutputSlotName));
        _window.OutputEjectButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.OutputSlotName));
        _window.BufferTransferButton.OnPressed += _ => SendPredictedMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Transfer));
        _window.BufferTransferButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Transfer));
        _window.BufferDiscardButton.OnPressed += _ => SendPredictedMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Discard));
        _window.BufferDiscardButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Discard));
        _window.CreatePillButton.OnPressed += _ => SendPredictedMessage(
            new ChemMasterCreatePillsMessage(
                (uint) _window.PillDosage.Value,
                (uint) _window.PillNumber.Value,
                _window.LabelLine,
                _window.ContainerLabelLine));
        _window.CreatePillButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterCreatePillsMessage(
                (uint) _window.PillDosageClassic.Value,
                (uint) _window.PillNumberClassic.Value,
                _window.LabelLine,
                _window.ContainerLabelLine));
        _window.CreatePatchButton.OnPressed += _ => SendPredictedMessage(
            new ChemMasterCreatePatchesMessage(
                (uint) _window.PatchDosage.Value,
                (uint) _window.PatchNumber.Value,
                _window.LabelLine,
                _window.ContainerLabelLine));
        _window.CreateBottleButton.OnPressed += _ => SendPredictedMessage(
            new ChemMasterOutputToBottleMessage(
                (uint) _window.BottleDosage.Value, _window.LabelLine));
        _window.CreateBottleButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterOutputToBottleMessage(
                (uint) _window.BottleDosageClassic.Value, _window.LabelLine));
        _window.CreatePatchButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterCreatePatchesMessage(
                (uint) _window.PatchDosageClassic.Value,
                (uint) _window.PatchNumberClassic.Value,
                _window.LabelLine,
                _window.ContainerLabelLine));
        _window.BufferSortButton.OnPressed += _ => SendPredictedMessage(
            new ChemMasterSortingTypeCycleMessage());
        _window.BufferSortButtonClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterSortingTypeCycleMessage());
        _window.OutputBufferDraw.OnPressed += _ => SendPredictedMessage(
            new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.Internal));
        _window.OutputBufferDrawClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.Internal));
        _window.OutputBeakerDraw.OnPressed += _ => SendPredictedMessage(
            new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.External));
        _window.OutputBeakerDrawClassic.OnPressed += _ => SendPredictedMessage(
            new ChemMasterOutputDrawSourceMessage(ChemMasterDrawSource.External));

        for (uint i = 0; i < _window.PillTypeButtons.Length; i++)
        {
            var pillType = i;
            _window.PillTypeButtons[i].OnPressed += _ => SendPredictedMessage(new ChemMasterSetPillTypeMessage(pillType));
            _window.PillTypeButtonsClassic[i].OnPressed += _ => SendPredictedMessage(new ChemMasterSetPillTypeMessage(pillType));
        }

        _window.OnReagentButtonPressed += (_, button) => SendPredictedMessage(new ChemMasterReagentAmountButtonMessage(button.Id, button.Amount, button.IsBuffer));
        _window.OnCustomReagentButtonPressed += (_, id, amount, isBuffer) => SendPredictedMessage(new ChemMasterReagentCustomAmountButtonMessage(id, amount, isBuffer));

        _window.OnAmountSelected += amount => SendPredictedMessage(new ChemMasterSetTransferAmountMessage(amount));

        _window.OnToggleValveButtonPressed += () =>
            SendPredictedMessage(new ChemMasterToggleValveMessage());
    }

    public override void Update() => _window?.DirtyData(ChemMasterUIDirtyFlags.All);

    public void Update(ChemMasterUIDirtyFlags flags) => _window?.DirtyData(flags);
}
