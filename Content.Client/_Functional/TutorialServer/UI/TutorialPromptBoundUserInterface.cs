using Content.Shared._Functional.TutorialServer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Functional.TutorialServer.UI;

[UsedImplicitly]
public sealed class TutorialPromptBoundUserInterface : BoundUserInterface
{
    private TutorialPromptWindow? _window;

    public TutorialPromptBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TutorialPromptWindow>();
        _window.OnNextPressed += () => SendMessage(new TutorialPromptNextBuiMsg());
        _window.OnHintPressed += () => SendMessage(new TutorialPromptHintBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not TutorialPromptBuiState s)
            return;

        _window.Populate(s);
    }
}
