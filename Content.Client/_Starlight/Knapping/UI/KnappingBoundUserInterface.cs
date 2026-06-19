using Content.Shared._Starlight.Knapping;

namespace Content.Client._Starlight.Knapping.UI;

public sealed class KnappingBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private KnappingWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = new KnappingWindow();
        _window.OnClose += Close;
        _window.OnTileSet += (x, y, filled) => SendMessage(new KnappingTileSetMessage(x, y, filled));
        _window.OnRecipeSelected += recipe => SendMessage(new KnappingRecipeSelectedMessage(recipe));
        _window.OnFinishPressed += () => SendMessage(new KnappingFinishMessage());

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not KnappingBoundUserInterfaceState cast)
            return;

        _window?.SetState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _window?.Close();
        _window = null;
    }
}
