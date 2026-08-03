using Content.Shared._Starlight.Medical.Virology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Medical.Virology;

[UsedImplicitly]
public sealed class PathogenDetectorBoundUserInterface(
    EntityUid owner,
    Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PathogenDetectorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PathogenDetectorWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is null || state is not PathogenDetectorUiState detectorState)
            return;

        _window.UpdateState(detectorState);
    }
}
