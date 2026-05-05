using Content.Shared._Starlight.Antags.Vampires;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
namespace Content.Client._Starlight.Antags.Vampires;

[UsedImplicitly]
public sealed class VampireLocateBui : BoundUserInterface
{
    private VampireLocateWindow? _window;

    public VampireLocateBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<VampireLocateWindow>();
        _window.TargetSelected += OnTargetSelected;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.TargetSelected -= OnTargetSelected;
        }

        base.Dispose(disposing);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not VampireLocateBuiState msg)
            return;

        _window?.SetTargets(msg.Targets);
    }

    private void OnTargetSelected(VampireLocateTarget target)
    {
        SendMessage(new VampireLocateSelectedBuiMsg { Target = target.Target });
        Close();
    }
}
