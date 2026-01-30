using System;
using System.Collections.Generic;
using Content.Shared.Silicons.StationAi;

namespace Content.Client._Starlight.Silicons.StationAi;

/// <summary>
/// Wraps the Station AI warp window lifecycle so UI logic can live in the Starlight namespace.
/// </summary>
public sealed class StationAiWarpUiController
{
    private StationAiWarpWindow? _warpWindow;
    private Action<StationAiWarpTarget>? _targetSelected;
    private Action? _windowClosed;

    public void EnsureWindow(Action<StationAiWarpTarget> onTargetSelected, Action onWindowClosed)
    {
        if (_warpWindow == null)
        {
            _warpWindow = new StationAiWarpWindow();
            _warpWindow.TargetSelected += HandleTargetSelected;
            _warpWindow.OnClose += HandleWindowClosed;
        }

        _targetSelected = onTargetSelected;
        _windowClosed = onWindowClosed;

        if (!_warpWindow.IsOpen)
            _warpWindow.OpenCentered();
    }

    public void SetLoading(bool loading)
    {
        _warpWindow?.SetLoading(loading);
    }

    public void SetTargets(IEnumerable<StationAiWarpTarget> targets)
    {
        _warpWindow?.SetTargets(targets);
    }

    public void CloseWindow()
    {
        _warpWindow?.Close();
    }

    public void ClearWindow()
    {
        if (_warpWindow == null)
            return;

        _warpWindow.TargetSelected -= HandleTargetSelected;
        _warpWindow.OnClose -= HandleWindowClosed;
        _warpWindow = null;
        _targetSelected = null;
        _windowClosed = null;
    }

    public void Dispose()
    {
        if (_warpWindow == null)
            return;

        _warpWindow.TargetSelected -= HandleTargetSelected;
        _warpWindow.OnClose -= HandleWindowClosed;
        _warpWindow.Close();
        _warpWindow = null;
        _targetSelected = null;
        _windowClosed = null;
    }

    private void HandleTargetSelected(StationAiWarpTarget target)
    {
        _targetSelected?.Invoke(target);
    }

    private void HandleWindowClosed()
    {
        _windowClosed?.Invoke();
    }
}
