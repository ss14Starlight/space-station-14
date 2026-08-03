using Content.Shared._Starlight.Medical.Virology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Medical.Virology;

[UsedImplicitly]
public sealed class PathogenAnalyzerBoundUserInterface(
    EntityUid owner,
    Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PathogenAnalyzerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PathogenAnalyzerWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is not null && state is PathogenAnalyzerUiState analyzerState)
            _window.UpdateState(analyzerState);
    }
}
