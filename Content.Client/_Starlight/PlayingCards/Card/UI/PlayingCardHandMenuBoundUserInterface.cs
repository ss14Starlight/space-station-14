using Content.Shared._Starlight.PlayingCards.Hand;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;

namespace Content.Client._Starlight.PlayingCards.Card.UI;

[UsedImplicitly]
public sealed class PlayingCardHandMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IClyde _displayManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;

    private PlayingCardHandMenu? _menu;

    public PlayingCardHandMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) =>
        IoCManager.InjectDependencies(this);
    
    protected override void Open()
    {
        base.Open();

        _menu = new(Owner, this);
        _menu.OnClose += Close;

        // Open the menu, centered on the mouse
        var vpSize = _displayManager.ScreenSize;
        _menu.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / vpSize);
    }

    public void SendCardHandDrawMessage(NetEntity e) => SendMessage(new PlayingCardHandDrawMessage(e));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        _menu?.Dispose();
    }
}