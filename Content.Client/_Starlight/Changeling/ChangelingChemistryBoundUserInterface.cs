using Content.Shared._Starlight.Changeling;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Changeling;

[UsedImplicitly]
public sealed class ChangelingChemistryBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ChangelingChemistryWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ChangelingChemistryWindow>();
        _window.OnAddReagent += (reagent, amount) => SendMessage(new ChangelingAddReagentMessage(reagent, amount));
        _window.OnClear += () => SendMessage(new ChangelingClearMixtureMessage());
        _window.OnTemperature += temperature => SendMessage(new ChangelingSetTemperatureMessage(temperature));
        _window.OnRegeneration += enabled => SendMessage(new ChangelingSetRegenerationMessage(enabled));
        _window.OnDose += dose => SendMessage(new ChangelingSetStingDoseMessage(dose));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ChangelingChemistryUiState chemistry)
            _window?.UpdateState(chemistry);
    }
}
