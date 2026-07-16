using Robust.Client.UserInterface;

namespace Content.Client._Starlight.UserInterface;

public abstract class PopOutBui<T>(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey) where T : Control, IPopOutWindow
{
    [ViewVariables]
    protected abstract T? Window { get; set; }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            Window?.DisposePopOut();
    }
}
