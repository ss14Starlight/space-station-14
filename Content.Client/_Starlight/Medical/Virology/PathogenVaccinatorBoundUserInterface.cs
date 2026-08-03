using Content.Shared._Starlight.Medical.Virology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Medical.Virology;

[UsedImplicitly]
public sealed class PathogenVaccinatorBoundUserInterface(
    EntityUid owner,
    Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PathogenVaccinatorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PathogenVaccinatorWindow>();
        _window.OnProduce += live => SendMessage(new PathogenVaccinatorProduceMessage(live));
        _window.OnEject += slot => SendMessage(new PathogenVaccinatorEjectMessage(slot));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is null || state is not PathogenVaccinatorUiState vaccinatorState)
            return;

        _window.UpdateState(vaccinatorState);
    }
}
