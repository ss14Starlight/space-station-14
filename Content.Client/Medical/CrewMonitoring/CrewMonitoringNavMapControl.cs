using Content.Client.Pinpointer.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface; // Starlight
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Map; // Starlight
using Robust.Shared.Localization; // Starlight

namespace Content.Client.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringNavMapControl : NavMapControl
{
    public NetEntity? Focus;
    public Dictionary<NetEntity, string> LocalizedNames = new();
    public bool ShowFocusedEntityPanel = true; // Starlight
    public bool AllowManualRecentering = true; // Starlight
    public event Action<EntityCoordinates>? MapClicked; // Starlight

    private Label _trackedEntityLabel;
    private PanelContainer _trackedEntityPanel;
    private Button? _recenterButton; // Starlight
    private readonly SharedTransformSystem _transformSystem; //FarHorizons

    public CrewMonitoringNavMapControl() : base()
    {
        WallColor = new Color(192, 122, 196);
        TileColor = new(71, 42, 72);
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(BackgroundOpacity));

        _trackedEntityLabel = new Label
        {
            Margin = new Thickness(10f, 8f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Modulate = Color.White,
        };

        _trackedEntityPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = BackgroundColor,
            },

            Margin = new Thickness(5f, 10f),
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Bottom,
            Visible = false,
        };

        _trackedEntityPanel.AddChild(_trackedEntityLabel);
        this.AddChild(_trackedEntityPanel);
        _transformSystem = EntManager.System<SharedTransformSystem>();//FarHorizons
        _recenterButton = TryGetRecenterButton();
        MapClickedAction += coords => MapClicked?.Invoke(coords); // Starlight
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!AllowManualRecentering)
        {
            Recentering = false;

            if (_recenterButton != null)
            {
                _recenterButton.Visible = false;
                _recenterButton.Disabled = true;
            }
        }

        if (!ShowFocusedEntityPanel)
        {
            _trackedEntityLabel.Text = string.Empty;
            _trackedEntityPanel.Visible = false;
            return;
        }

        if (Focus == null)
        {
            _trackedEntityLabel.Text = string.Empty;
            _trackedEntityPanel.Visible = false;

            return;
        }

        foreach ((var netEntity, var blip) in TrackedEntities)
        {
            if (netEntity != Focus)
                continue;

            if (!LocalizedNames.TryGetValue(netEntity, out var name))
                name = Loc.GetString("navmap-unknown-entity");

            var message = name + "\n" + Loc.GetString("navmap-location",
                ("x", MathF.Round(_transformSystem.ToMapCoordinates(blip.Coordinates).X)), //FarHorizons
                ("y", MathF.Round(_transformSystem.ToMapCoordinates(blip.Coordinates).Y)));//FarHorizons

            _trackedEntityLabel.Text = message;
            _trackedEntityPanel.Visible = true;

            return;
        }

        _trackedEntityLabel.Text = string.Empty;
        _trackedEntityPanel.Visible = false;
    }

    private Button? TryGetRecenterButton()
    {
        if (TryGetFirstChild(this) is not BoxContainer topContainer)
            return null;

        if (TryGetFirstChild(topContainer) is not PanelContainer topPanel)
            return null;

        if (TryGetFirstChild(topPanel) is not BoxContainer header)
            return null;

        foreach (var child in header.Children)
        {
            if (child is Button button)
                return button;
        }

        return null;
    }

    private static Control? TryGetFirstChild(Control control)
    {
        foreach (var child in control.Children)
        {
            return child;
        }

        return null;
    }
}
