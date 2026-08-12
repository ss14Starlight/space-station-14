using Content.Shared._Starlight.Scent;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Scent;

public sealed class ScentSniffBoundUserInterface : BoundUserInterface
{
    private ScentSniffMenu? _window;

    public ScentSniffBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ScentSniffMenu>();
        _window.OnTrackPressed += OnTrackPressed;
    }

    private void OnTrackPressed(string scentId)
    {
        SendMessage(new ScentSniffTrackMessage(scentId));
        Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not ScentSniffBoundUserInterfaceState cast)
            return;

        _window.UpdateEntries(cast.Entries);
    }
}
