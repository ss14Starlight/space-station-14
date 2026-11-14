using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared._Starlight.Breadcrumb.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Breadcrumb.UI;

[UsedImplicitly]
public sealed partial class BreadcrumbDropperBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    [ViewVariables] private BreadcrumbDropperWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BreadcrumbDropperWindow>();
        _window.OnSubmit += OnSubmitPressed;

        if (EntMan.TryGetComponent<BreadcrumbDropperComponent>(Owner, out var dropper))
            _window.SetOptions(dropper.ColorOptions, dropper.Color);
    }

    private void OnSubmitPressed()
    {
        if (_window?.SelectedColor is not { } key)
            return;

        SendMessage(new BreadcrumbDropperColorMessage(key));
        _window.Close();
    }
}
