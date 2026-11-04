// ReSharper disable CheckNamespace

using Content.Shared.Shuttles.BUIStates;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    private ShuttleSurfaceScreen _surfaceScreen = default!;
    private Button _surfaceModeButton = default!;

    public event Action<EntityCoordinates>? RadarClicked;

    partial void InitializeStarlight()
    {
        NavContainer.OnRadarClick += OnRadarClick;

        _surfaceModeButton = new Button
        {
            Text = Loc.GetString("shuttle-console-surface-mode"),
            ToggleMode = true,
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
            Group = NavModeButton.Group
        };
        _surfaceModeButton.OnPressed += _ => SwitchMode(ShuttleConsoleMode.Surface);
        ModeButtons.AddChild(_surfaceModeButton);

        _surfaceScreen = new ShuttleSurfaceScreen
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
        };
        _surfaceScreen.ReturnRequested += OnSurfaceReturn;
        Contents.AddChild(_surfaceScreen);
    }

    partial void UpdateSurfaceState(EntityCoordinates? coordinates, ShuttleBoundUserInterfaceState cState)
    {
        EntityUid? mapGrid = null;
        if (cState.SurfaceMapGrid is { } netMapGrid)
            _entManager.TryGetEntity(netMapGrid, out mapGrid);
        _surfaceScreen.UpdateMap(mapGrid, coordinates);
        _surfaceScreen.UpdateFTLStatus(cState.MapState);

        NavModeButton.Visible = !cState.SurfaceMode;
        MapModeButton.Visible = !cState.SurfaceMode;
        DockModeButton.Visible = !cState.SurfaceMode;
        _surfaceModeButton.Visible = cState.SurfaceMode;

        if (cState.SurfaceMode)
        {
            _surfaceModeButton.Pressed = true;
            if (_mode != ShuttleConsoleMode.Surface)
                SwitchMode(ShuttleConsoleMode.Surface);
        }
        else if (_mode == ShuttleConsoleMode.Surface)
        {
            _surfaceModeButton.Pressed = false;
            NavModeButton.Pressed = true;
            SwitchMode(ShuttleConsoleMode.Nav);
        }
    }

    private void OnRadarClick(EntityCoordinates coordinates)
        => RadarClicked?.Invoke(coordinates);

    private void OnSurfaceReturn()
        => SurfaceReturnRequested?.Invoke();
}