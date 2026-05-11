using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Starlight.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Starlight.Shuttles;

[UsedImplicitly]
public sealed class DropPodConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Button? _deployButton;
    private Label? _statusLabel;
    private Label? _selectedLabel;
    private readonly Dictionary<DropPodDirection, Button> _dirButtons = new();
    private DropPodNavMapControl? _mapControl;

    private DropPodDirection? _selectedDirection;
    private HashSet<DropPodDirection> _available = new();
    private bool _canLaunch;
    private bool _alreadyLaunched;

    protected override void Open()
    {
        base.Open();
        TryInitWindow();
        UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state is not DropPodConsoleBuiState s)
            return;

        TryInitWindow();

        _available = s.AvailableDirections;
        _canLaunch = s.CanLaunch;
        _alreadyLaunched = s.AlreadyLaunched;

        // Push station map data to the map control
        if (_mapControl != null)
        {
            if (s.StationGrid.HasValue)
            {
                var stationUid = IoCManager.Resolve<IEntityManager>().GetEntity(s.StationGrid.Value);
                if (_mapControl.MapUid != stationUid)
                {
                    _mapControl.MapUid = stationUid;
                    _mapControl.ForceNavMapUpdate();
                    _mapControl.CenterOnWorldPos(stationUid, s.StationWorldCenter);
                }
            }
        }

        // Deselect if chosen sector is no longer available
        if (_selectedDirection.HasValue && !_available.Contains(_selectedDirection.Value))
            _selectedDirection = null;

        RefreshState();

        _statusLabel!.Text = _alreadyLaunched
            ? Loc.GetString("drop-pod-console-status-launched")
            : !_canLaunch
                ? Loc.GetString("drop-pod-console-status-not-ready")
                : Loc.GetString("drop-pod-console-status-ready");

        _selectedLabel!.Text = _selectedDirection.HasValue
            ? DirectionLabel(_selectedDirection.Value)
            : Loc.GetString("drop-pod-console-sector-none");

        if (!_window!.IsOpen)
            _window.OpenCentered();
    }

    private void TryInitWindow()
    {
        if (_window != null)
            return;

        _window = new DefaultWindow
        {
            Title = Loc.GetString("drop-pod-console-title"),
            MinSize = new Vector2(620, 400),
            Resizable = false,
        };

        // Main layout: left controls | right map
        var main = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8),
        };

        // ---- Left panel ----
        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinSize = new Vector2(185, 0),
            Margin = new Thickness(0, 0, 10, 0),
        };

        _statusLabel = new Label
        {
            Margin = new Thickness(0, 0, 0, 8),
        };

        // Compass 3x3 grid: [  ][N][  ] / [W][  ][E] / [  ][S][  ]
        _dirButtons[DropPodDirection.North] = MakeDirButton("N", DropPodDirection.North);
        _dirButtons[DropPodDirection.East]  = MakeDirButton("E", DropPodDirection.East);
        _dirButtons[DropPodDirection.South] = MakeDirButton("S", DropPodDirection.South);
        _dirButtons[DropPodDirection.West]  = MakeDirButton("W", DropPodDirection.West);

        var compass = new GridContainer
        {
            Columns = 3,
            Margin = new Thickness(0, 0, 0, 8),
        };
        compass.AddChild(new Control { MinSize = new Vector2(55, 55) });
        compass.AddChild(_dirButtons[DropPodDirection.North]);
        compass.AddChild(new Control { MinSize = new Vector2(55, 55) });
        compass.AddChild(_dirButtons[DropPodDirection.West]);
        compass.AddChild(new Control { MinSize = new Vector2(55, 55) }); // center gap
        compass.AddChild(_dirButtons[DropPodDirection.East]);
        compass.AddChild(new Control { MinSize = new Vector2(55, 55) });
        compass.AddChild(_dirButtons[DropPodDirection.South]);
        compass.AddChild(new Control { MinSize = new Vector2(55, 55) });

        var sectorHeader = new Label
        {
            Text = "CURRENT SELECTED SECTOR",
            Margin = new Thickness(0, 0, 0, 2),
        };

        _selectedLabel = new Label
        {
            Margin = new Thickness(0, 0, 0, 6),
        };

        var notice = new Label
        {
            Text = Loc.GetString("drop-pod-console-notice"),
            FontColorOverride = Color.Yellow,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 8),
        };

        _deployButton = new Button
        {
            Text = Loc.GetString("drop-pod-console-deploy-button"),
            Disabled = true,
            HorizontalExpand = true,
            ModulateSelfOverride = Color.Red,
        };
        _deployButton.OnPressed += _ => OnDeployPressed();

        left.AddChild(_statusLabel);
        left.AddChild(compass);
        left.AddChild(sectorHeader);
        left.AddChild(_selectedLabel);
        left.AddChild(new Control { VerticalExpand = true }); // spacer
        left.AddChild(notice);
        left.AddChild(_deployButton);

        // ---- Right panel: interactive station map ----
        _mapControl = new DropPodNavMapControl
        {
            SetWidth = 400,
            SetHeight = 350,
            RectClipContent = true,
        };

        main.AddChild(left);
        main.AddChild(_mapControl);

        _window.Contents.AddChild(main);
        _window.OnClose += Close;
    }

    private Button MakeDirButton(string text, DropPodDirection dir)
    {
        var btn = new Button
        {
            Text = text,
            MinSize = new Vector2(55, 55),
            Disabled = true,
        };
        btn.OnPressed += _ => SelectDirection(dir);
        return btn;
    }

    private void SelectDirection(DropPodDirection dir)
    {
        if (!_available.Contains(dir) || !_canLaunch || _alreadyLaunched)
            return;
        _selectedDirection = dir;
        RefreshState();

        _selectedLabel!.Text = DirectionLabel(dir);
    }

    private void RefreshState()
    {
        if (_mapControl != null)
            _mapControl.SelectedDirection = _selectedDirection;

        foreach (var (d, btn) in _dirButtons)
        {
            btn.Disabled = !_available.Contains(d) || !_canLaunch || _alreadyLaunched;
            btn.ModulateSelfOverride = _selectedDirection == d ? Color.OrangeRed : null;
        }

        _deployButton!.Disabled = _selectedDirection == null || !_canLaunch || _alreadyLaunched;
    }

    private static string DirectionLabel(DropPodDirection dir) => dir switch
    {
        DropPodDirection.North => "NORTH",
        DropPodDirection.East  => "EAST",
        DropPodDirection.South => "SOUTH",
        DropPodDirection.West  => "WEST",
        _                      => "UNKNOWN",
    };

    private void OnDeployPressed()
    {
        if (_selectedDirection == null)
            return;
        SendMessage(new DropPodConsoleDeployMessage { Direction = _selectedDirection.Value });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
