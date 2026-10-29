// ReSharper disable CheckNamespace

using System.Numerics;
using Content.Client.Pinpointer.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Timing;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleSurfaceScreen : BoxContainer
{
    private readonly NavMapControl _map;
    private readonly Label _ftlStateLabel;
    private readonly ProgressBar _ftlBar;
    private readonly StyleBoxFlat _ftlStyle;
    private StartEndTime _ftlTime;

    [Dependency] private IGameTiming _timing = default!;

    public event Action? ReturnRequested;

    public ShuttleSurfaceScreen()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Horizontal;

        _map = new NavMapControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var controls = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Top,
            VerticalExpand = true,
            MinSize = new Vector2(256f, 0f),
        };

        _ftlStateLabel = new Label
        {
            Text = Loc.GetString("shuttle-console-ftl-state-Available"),
            VerticalAlignment = VAlignment.Stretch,
            HorizontalAlignment = HAlignment.Center,
        };
        _ftlStyle = new StyleBoxFlat(Color.LimeGreen);
        _ftlBar = new ProgressBar
        {
            HorizontalExpand = true,
            Margin = new Thickness(5f),
            MinValue = 0f,
            MaxValue = 1f,
            Value = 1f,
            ForegroundStyleBoxOverride = _ftlStyle,
        };

        var returnButton = new ConfirmButton
        {
            HorizontalExpand = true,
            MinSize = new Vector2(256f, 48f),
            Text = Loc.GetString("shuttle-console-surface-return"),
            TextAlign = Label.AlignMode.Center,
        };
        returnButton.OnPressed += _ => ReturnRequested?.Invoke();

        controls.AddChild(CreateSectionHeader(Loc.GetString("shuttle-console-ftl-label")));
        controls.AddChild(_ftlStateLabel);
        controls.AddChild(_ftlBar);
        controls.AddChild(CreateSectionHeader(Loc.GetString("shuttle-console-map-settings")));
        controls.AddChild(returnButton);
        AddChild(_map);
        AddChild(controls);
    }

    /// <summary>
    /// Displays the specified map grid and optionally marks tracked coordinates.
    /// </summary>
    /// <param name="mapGrid">The entity UID of the grid to display.</param>
    /// <param name="trackedCoordinates">Coordinates to mark on the map, if any.</param>
    public void UpdateMap(EntityUid? mapGrid, EntityCoordinates? trackedCoordinates)
    {
        if (_map.MapUid != mapGrid)
        {
            _map.MapUid = mapGrid;
            _map.ForceNavMapUpdate();
        }

        _map.TrackedCoordinates.Clear();
        if (trackedCoordinates is { } coordinates)
            _map.TrackedCoordinates.Add(coordinates, (true, Color.Cyan));
    }

    /// <summary>
    /// Updates the FTL state label, its color, and the timing used by the progress bar.
    /// </summary>
    /// <param name="state">The shuttle map state containing the FTL status and timing.</param>
    public void UpdateFTLStatus(ShuttleMapInterfaceState state)
    {
        _ftlTime = state.FTLTime;
        _ftlStateLabel.Text = Loc.GetString($"shuttle-console-ftl-state-{state.FTLState}");

        _ftlStyle.BackgroundColor = state.FTLState switch
        {
            FTLState.Available => Color.FromHex("#80C71F"),
            FTLState.Starting => Color.FromHex("#169C9C"),
            FTLState.Travelling => Color.FromHex("#8932B8"),
            FTLState.Arriving => Color.FromHex("#F9801D"),
            _ => Color.FromHex("#B02E26"),
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var progress = _ftlTime.ProgressAt(_timing.CurTime);
        _ftlBar.Value = float.IsFinite(progress) ? progress : 1f;
    }

    private static StripeBack CreateSectionHeader(string text)
    {
        var header = new StripeBack
        {
            MinSize = new Vector2(48f, 48f),
            HorizontalExpand = true,
        };
        header.AddChild(new Label
        {
            Text = text,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
        });
        return header;
    }
}
