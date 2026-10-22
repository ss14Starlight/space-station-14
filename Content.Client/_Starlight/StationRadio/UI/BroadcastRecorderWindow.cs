using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.StationRadio;
using Content.Shared._Starlight.StationRadio.Components;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class BroadcastRecorderWindow : FancyWindow
{
    private const string IconPath = "/Textures/_Starlight/Interface/StationRadio/";

    private static readonly Color RecordingColor = Color.FromHex("#e0484f");
    private static readonly Color PlayingColor = Color.FromHex("#4fc257");
    private static readonly Color DimColor = Color.FromHex("#999696");

    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();

    private readonly Label _tape;
    private readonly Label _mode;
    private readonly Label _status;
    private readonly Label _time;
    private readonly PixelProgressBar _progress;
    private readonly Button _record;
    private readonly Button _play;
    private readonly Button _stop;
    private readonly Button _rewind;

    private BroadcastRecorderState? _state;

    public event Action<BroadcastRecorderAction>? OnAction;

    public BroadcastRecorderWindow()
    {
        Title = Loc.GetString("broadcast-recorder-title");
        MinSize = SetSize = new Vector2(320, 190);

        _tape = new Label { ClipText = true };
        _mode = new Label { HorizontalAlignment = HAlignment.Right };
        _status = new Label
        {
            Text = Loc.GetString("broadcast-recorder-no-server"),
            FontColorOverride = RecordingColor,
            StyleClasses = { StyleNano.StyleClassLabelSmall },
            Visible = false,
        };
        _progress = new PixelProgressBar { MinHeight = 8 };
        _time = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = DimColor,
        };

        _rewind = IconButton("icon_rewind.png", "broadcast-recorder-rewind", BroadcastRecorderAction.Rewind);
        _record = IconButton("icon_record.png", "broadcast-recorder-record", BroadcastRecorderAction.Record);
        _record.Modulate = RecordingColor;
        _play = IconButton("icon_play.png", "broadcast-recorder-play", BroadcastRecorderAction.Play);
        _stop = IconButton("icon_stop.png", "broadcast-recorder-stop", BroadcastRecorderAction.Stop);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _tape.HorizontalExpand = true;
        header.AddChild(_tape);
        header.AddChild(_mode);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 6,
        };
        buttons.AddChild(_rewind);
        buttons.AddChild(_record);
        buttons.AddChild(_play);
        buttons.AddChild(_stop);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };
        root.AddChild(header);
        root.AddChild(_status);
        root.AddChild(_progress);
        root.AddChild(_time);
        root.AddChild(buttons);
        ContentsContainer.AddChild(root);
    }

    public void UpdateState(BroadcastRecorderState state)
    {
        _state = state;
        var hasTape = state.Tape != null;

        _tape.Text = state.Tape ?? Loc.GetString("broadcast-recorder-no-tape");
        _status.Visible = !state.Connected;
        _mode.Text = Loc.GetString(state.Mode switch
        {
            BroadcastRecorderMode.Recording => "broadcast-recorder-mode-recording",
            BroadcastRecorderMode.Playing => "broadcast-recorder-mode-playing",
            _ => "broadcast-recorder-mode-stopped",
        });
        _mode.FontColorOverride = state.Mode switch
        {
            BroadcastRecorderMode.Recording => RecordingColor,
            BroadcastRecorderMode.Playing => PlayingColor,
            _ => DimColor,
        };

        var stopped = state.Mode == BroadcastRecorderMode.Stopped;
        _record.Disabled = !hasTape || !state.Connected || state.Mode == BroadcastRecorderMode.Recording;
        _play.Disabled = !hasTape || !state.Connected || state.Length <= TimeSpan.Zero || state.Mode == BroadcastRecorderMode.Playing;
        _stop.Disabled = stopped;
        _rewind.Disabled = !hasTape;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_state is not { } state || state.Tape == null)
        {
            _progress.Progress = 0;
            _time.Text = string.Empty;
            return;
        }

        var position = state.Mode == BroadcastRecorderMode.Stopped ? state.Position : _timing.CurTime - state.Start;
        if (position < TimeSpan.Zero)
            position = TimeSpan.Zero;

        var end = state.Mode == BroadcastRecorderMode.Recording ? state.MaxLength : state.Length;
        if (end > TimeSpan.Zero && position > end)
            position = end;

        _progress.Paused = state.Mode != BroadcastRecorderMode.Playing;
        _progress.Progress = end > TimeSpan.Zero ? (float) (position / end) : 0f;
        _time.Text = $"{FormatTime(position)} / {FormatTime(end)}";
    }

    private Button IconButton(string icon, string tooltip, BroadcastRecorderAction action)
    {
        var button = new Button
        {
            MinSize = new Vector2(40, 30),
            ToolTip = Loc.GetString(tooltip),
            StyleClasses = { StyleNano.ButtonOpenBoth },
        };
        button.AddChild(new TextureRect
        {
            Texture = IoCManager.Resolve<IResourceCache>().GetTexture(IconPath + icon),
            TextureScale = new Vector2(2, 2),
            Stretch = TextureRect.StretchMode.KeepCentered,
        });
        button.OnPressed += _ => OnAction?.Invoke(action);
        return button;
    }

    private static string FormatTime(TimeSpan time) => $"{(int) time.TotalMinutes}:{time.Seconds:00}";
}
