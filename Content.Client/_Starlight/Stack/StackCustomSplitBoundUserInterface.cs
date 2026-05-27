using Content.Shared._Starlight.Stack;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Stack;

[UsedImplicitly]
public sealed class StackCustomSplitBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables] private StackCustomSplitAmountWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StackCustomSplitAmountWindow>();

        if (EntMan.TryGetComponent<StackComponent>(Owner, out var stack))
            _window.SetBounds(1, stack.Count);

        _window.ApplyButton.OnPressed += _ =>
        {
            if (int.TryParse((string?)_window.AmountLineEdit.Text, out var i))
            {
                SendPredictedMessage(new StackCustomSplitMessage(i));
                _window.Close();
            }
        };
    }
}
