using Content.Client._Starlight.Zones.UI;
using Content.Client.Gameplay;
using Content.Client.Sandbox;
using Content.Shared._Starlight.Zones;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.UserInterface.Systems.ZonePlacer;

public sealed partial class ZonePlacerUIController : UIController, IOnStateExited<GameplayState>, IOnSystemChanged<SandboxSystem>
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [UISystemDependency] private readonly SandboxSystem _sandbox = default!;

    private ZonePlacerWindow? _window;

    /// <summary>
    /// Toggles the zone placer window. If the window is open, it will be closed. If the window is closed and sandbox mode is allowed, it will be opened.
    /// </summary>
    public void ToggleWindow()
    {
        EnsureWindow();

        if (_window!.IsOpen)
            _window.Close();
        else if (_sandbox.SandboxAllowed)
            _window.Open();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window == null)
            return;

        _window.Close();
        _window = null;
    }

    public void OnSystemLoaded(SandboxSystem system)
    {
        _sandbox.SandboxDisabled += CloseWindow;
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
    }

    public void OnSystemUnloaded(SandboxSystem system)
    {
        _sandbox.SandboxDisabled -= CloseWindow;
        _prototypes.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<ZonePrototype>())
            ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        if (_window != null && !_window.Disposed)
            _window.Populate(_prototypes.EnumeratePrototypes<ZonePrototype>());
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<ZonePlacerWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterLeft);
        ReloadPrototypes();
    }

    private void CloseWindow()
    {
        if (_window != null && !_window.Disposed)
            _window.Close();
    }
}
