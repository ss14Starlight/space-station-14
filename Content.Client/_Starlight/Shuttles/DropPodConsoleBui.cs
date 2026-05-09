using System.Numerics;
using Content.Shared._Starlight.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Shuttles;

[UsedImplicitly]
public sealed class DropPodConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private ItemList? _beaconList;
    private Button? _deployButton;
    private Label? _statusLabel;

    private List<DropPodBeaconEntry> _beacons = new();
    private int _selectedIdx = -1;

    protected override void Open()
    {
        base.Open();
        TryInitWindow();
        UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state is not DropPodConsoleBuiState buiState)
            return;

        TryInitWindow();

        _beacons = buiState.Beacons;

        _beaconList!.Clear();
        foreach (var beacon in _beacons)
        {
            _beaconList.AddItem(beacon.Name);
        }

        if (buiState.AlreadyLaunched)
        {
            _statusLabel!.Text = Loc.GetString("drop-pod-console-status-launched");
            _deployButton!.Disabled = true;
        }
        else if (!buiState.CanLaunch)
        {
            _statusLabel!.Text = Loc.GetString("drop-pod-console-status-not-ready");
            _deployButton!.Disabled = true;
        }
        else
        {
            _statusLabel!.Text = Loc.GetString("drop-pod-console-status-ready");
            _deployButton!.Disabled = _selectedIdx == -1;
        }

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
            MinSize = new Vector2(380, 320),
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        _statusLabel = new Label
        {
            Text = string.Empty,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var listLabel = new Label
        {
            Text = Loc.GetString("drop-pod-console-beacon-list-label"),
            Margin = new Thickness(0, 0, 0, 2),
        };

        _beaconList = new ItemList
        {
            VerticalExpand = true,
            SelectMode = ItemList.ItemListSelectMode.Single,
        };
        _beaconList.OnItemSelected += args =>
        {
            _selectedIdx = args.ItemIndex;
            if (_deployButton != null)
                _deployButton.Disabled = false;
        };
        _beaconList.OnItemDeselected += _ =>
        {
            _selectedIdx = -1;
            if (_deployButton != null)
                _deployButton.Disabled = true;
        };

        _deployButton = new Button
        {
            Text = Loc.GetString("drop-pod-console-deploy-button"),
            Margin = new Thickness(0, 6, 0, 0),
            Disabled = true,
            ModulateSelfOverride = Color.Red,
        };
        _deployButton.OnPressed += _ => OnDeployPressed();

        root.AddChild(_statusLabel);
        root.AddChild(listLabel);
        root.AddChild(_beaconList);
        root.AddChild(_deployButton);

        _window.Contents.AddChild(root);
        _window.OnClose += Close;
    }

    private void OnDeployPressed()
    {
        if (_beaconList == null || _selectedIdx == -1)
            return;

        if (_selectedIdx < 0 || _selectedIdx >= _beacons.Count)
            return;

        var selected = _beacons[_selectedIdx];
        SendMessage(new DropPodConsoleDeployMessage { SelectedBeacon = selected.Beacon });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
    }
}
