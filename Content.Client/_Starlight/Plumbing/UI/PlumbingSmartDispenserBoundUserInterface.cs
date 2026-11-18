using Content.Shared._Starlight.Plumbing;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Plumbing.UI;

[UsedImplicitly]
public sealed class PlumbingSmartDispenserBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PlumbingSmartDispenserWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PlumbingSmartDispenserWindow>();
        _window.SetOwner(Owner);

        _window.AmountGrid.OnButtonPressed += value => SendPredictedMessage(new PlumbingSmartDispenserSetDispenseAmountMessage(value));
        _window.OnClearPressed += (target) => SendPredictedMessage(new PlumbingSmartDispenserClearSolutionMessage(target));
        _window.OnDispenseReagentPressed += (reagentId, amount, target) => SendPredictedMessage(new PlumbingSmartDispenserDispenseReagentMessage(reagentId, amount, target));
    }

    public override void Update()
    {
        base.Update();
        _window?.SetLocalDirty();
    }
}
