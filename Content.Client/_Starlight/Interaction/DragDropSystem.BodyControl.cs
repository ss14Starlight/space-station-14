using Content.Client._Starlight.BodyControl;

// ReSharper disable CheckNamespace
namespace Content.Client.Interaction;

public sealed partial class DragDropSystem
{
    [Dependency] private BodyControlViewSystem _bodyView = default!;

    private EntityUid? DragUiEntity => _bodyView.Entity;
}
