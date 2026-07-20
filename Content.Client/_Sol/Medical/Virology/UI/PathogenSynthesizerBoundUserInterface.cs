using Content.Shared._Sol.Medical.Virology;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Sol.Medical.Virology.UI;

[UsedImplicitly]
public sealed class PathogenSynthesizerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PathogenSynthesizerWindow? _window;

    private PathogenSynthesizerBoundUserInterfaceState? _pending;

    public PathogenSynthesizerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PathogenSynthesizerWindow>();
        _window.OnStart += () => SendMessage(new PathogenSynthesizerStartMessage());
        _window.OnClearSelection += () => SendMessage(new PathogenSynthesizerClearSelectionMessage());
        _window.OnToggleGene += netEnt => SendMessage(new PathogenSynthesizerToggleGeneMessage(netEnt));
        _window.OnSubstratePressed += () =>
            SendMessage(new ItemSlotButtonPressedEvent(SharedPathogenSynthesizer.SubstrateSlotId));

        if (_pending != null)
            _window.UpdateState(_pending);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PathogenSynthesizerBoundUserInterfaceState sState)
            return;

        _pending = sState;
        _window?.UpdateState(sState);
    }
}
