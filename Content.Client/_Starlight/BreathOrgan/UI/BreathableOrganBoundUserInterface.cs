using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.BreathOrgan.UI;

/// <summary>
/// Copy of the gas tank user interface made specifically for organ gas tanks.
/// Contains a button to empty the organ replacing the text field to edit the output pressure
/// </summary>
[UsedImplicitly]
public sealed class BreathableOrganBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private OrganGasTankWindow? _window;

    public void ToggleInternals()
        => SendPredictedMessage(new GasTankToggleInternalsMessage());

    public void EmptyOrgan()
        => SendPredictedMessage(new GasTankEmptyOrganMessage());

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrganGasTankWindow>();
        _window.Entity = Owner;
        _window.SetTitle(EntMan.GetComponent<MetaDataComponent>(Owner).EntityName);
        _window.OnToggleInternals += ToggleInternals;
        _window.OnEmptyOrgan += EmptyOrgan;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (EntMan.TryGetComponent(Owner, out GasTankComponent? component))
        {
            var canConnect = EntMan.System<SharedGasTankSystem>().CanConnectToInternals((Owner, component));
            _window?.Update(canConnect, component.IsConnected, component.OutputPressure);
        }

        if (state is GasTankBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _window?.Close();
    }
}

