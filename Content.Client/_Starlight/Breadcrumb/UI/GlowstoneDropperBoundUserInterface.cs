using Content.Shared._Starlight.Breadcrumb.Components;
using Content.Shared._Starlight.Breadcrumb.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Breadcrumb.UI;

[UsedImplicitly]
public sealed partial class GlowstoneDropperBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    [ViewVariables] private GlowstoneDropperWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GlowstoneDropperWindow>();
        _window.OnSubmit += OnSubmitPressed;

        // No live state sync - read the dropper's YAML-defined options and current (networked) color so the
        // dropdown is populated and opens on what's currently set.
        if (EntMan.TryGetComponent<BreadcrumbDropperComponent>(Owner, out var dropper))
            _window.SetOptions(dropper.ColorOptions, dropper.Color);
    }

    private void OnSubmitPressed()
    {
        if (_window?.CurrentColorKey is not { } key)
            return;

        SendMessage(new GlowstoneDropperColorMessage(key));
        _window.Close();
    }
}
