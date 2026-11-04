using Content.Client.UserInterface.Controls;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Client._Starlight.Chemistry.UI;

// ReSharper disable CheckNamespace
namespace Content.Client.Chemistry.UI;

/// <summary>
/// Initializes a <see cref="ReagentDispenserWindow"/> and updates it when new server messages are received.
/// </summary>
[UsedImplicitly]
public sealed class ReagentDispenserBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ReagentDispenserWindow? _window;

    /// <summary>
    /// Called each time a dispenser UI instance is opened. Generates the dispenser window and fills it with
    /// relevant info. Sets the actions for static buttons.
    /// <para>Buttons which can change like reagent dispense buttons have their actions set in <see cref="UpdateReagentsList"/>.</para>
    /// </summary>
    protected override void Open()
    {
        base.Open();

        // Setup window layout/elements
        _window = this.CreateWindow<ReagentDispenserWindow>();
        _window.SetInfoFromEntity(EntMan, Owner);
        _window.SetOwner(Owner);

        // Setup static button actions.
        _window.EjectButton.OnPressed += _ => SendPredictedMessage(new ItemSlotButtonPressedEvent(SharedReagentDispenser.OutputSlotName));
        _window.ClearButton.OnPressed += _ => SendPredictedMessage(new ReagentDispenserClearContainerSolutionMessage());
        _window.AmountGrid.OnButtonPressed += s => SendPredictedMessage(new ReagentDispenserSetDispenseAmountMessage(s));
        _window.OnDispenseReagentButtonPressed += (data) => SendPredictedMessage(new ReagentDispenserDispenseReagentMessage(data));
        _window.OnEjectJugButtonPressed += (location) => SendPredictedMessage(new ReagentDispenserEjectContainerMessage(location));
        _window.OnToggleValveButtonPressed += () => SendPredictedMessage(new ReagentDispenserToggleValveMessage());
    }

    public override void Update()
    {
        base.Update();
        _window?.UpdateData();
    }
}
