using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.StationRadio;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.StationRadio.UI;

public sealed class StationRadioMixerWindow : FancyWindow
{
    private static readonly Color StripColor = Color.FromHex("#1b1c21");
    private static readonly Color StripBorder = Color.FromHex("#3d404b");
    private static readonly Color MasterColor = Color.FromHex("#24191b");
    private static readonly Color LedOn = Color.FromHex("#4fc257");
    private static readonly Color LedCued = Color.FromHex("#e3c447");
    private static readonly Color LedOff = Color.FromHex("#23302a");
    private static readonly Color DiscColor = Color.FromHex("#999696");

    private const string PlayIcon = "/Textures/_Starlight/Interface/StationRadio/icon_play.png";
    private const string PauseIcon = "/Textures/_Starlight/Interface/StationRadio/icon_pause.png";
    private const string RewindIcon = "/Textures/_Starlight/Interface/StationRadio/icon_rewind.png";
    private const string NextIcon = "/Textures/_Starlight/Interface/StationRadio/icon_next.png";
    private const string EjectIcon = "/Textures/_Starlight/Interface/StationRadio/icon_eject.png";
    private static readonly Color CurrentTrackColor = Color.FromHex("#4fc257");

    private readonly BoxContainer _sources;
    private readonly BoxContainer _playlists;
    private readonly ScrollContainer _playlistColumn;
    private readonly Label _status;
    private readonly Label _listeners;
    private readonly Label _interference;
    private readonly ChannelStrip _voice;
    private readonly ChannelStrip _master;
    private readonly ChannelStrip _monitor;
    private readonly Dictionary<NetEntity, ChannelStrip> _sourceStrips = new();

    public event Action<StationRadioMixerFader, NetEntity?, float, bool>? OnFaderChanged;

    public event Action<NetEntity, StationRadioMixerTransport, int>? OnTransport;

    public StationRadioMixerWindow()
    {
        Title = Loc.GetString("station-radio-mixer-title");
        MinSize = new Vector2(420, 560);
        SetSize = new Vector2(WindowWidth(2, true), WindowHeight);

        _status = new Label
        {
            Text = Loc.GetString("station-radio-mixer-no-server"),
            FontColorOverride = Color.FromHex("#e0484f"),
            HorizontalAlignment = HAlignment.Center,
            Visible = false,
        };

        _listeners = new Label { FontColorOverride = DiscColor, HorizontalExpand = true };
        _interference = new Label
        {
            Text = Loc.GetString("station-radio-mixer-interference"),
            FontColorOverride = Color.FromHex("#e3c447"),
            Visible = false,
        };

        _sources = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            VScrollEnabled = false,
        };
        scroll.AddChild(_sources);

        _voice = new ChannelStrip(Loc.GetString("station-radio-mixer-voice"), StripColor, false);
        _voice.Fader.OnValueChanged += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Voice, null, f.Value, false);
        _voice.Fader.OnReleased += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Voice, null, f.Value, true);

        _master = new ChannelStrip(Loc.GetString("station-radio-mixer-master"), MasterColor, false);
        _master.Fader.OnValueChanged += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Master, null, f.Value, false);
        _master.Fader.OnReleased += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Master, null, f.Value, true);

        _monitor = new ChannelStrip(Loc.GetString("station-radio-mixer-monitor"), StripColor, false) { Visible = false };
        _monitor.Fader.OnValueChanged += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Monitor, null, f.Value, false);
        _monitor.Fader.OnReleased += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Monitor, null, f.Value, true);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };
        row.AddChild(scroll);
        row.AddChild(new PanelContainer
        {
            MinWidth = 2,
            PanelOverride = new StyleBoxFlat { BackgroundColor = StripBorder },
        });
        row.AddChild(_voice);
        row.AddChild(_monitor);
        row.AddChild(_master);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
        };

        _playlists = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        _playlistColumn = new ScrollContainer
        {
            HScrollEnabled = false,
            MinWidth = PlaylistWidth,
            MaxWidth = PlaylistWidth,
            Margin = new Thickness(0, 8, 8, 8),
            Visible = false,
        };
        _playlistColumn.AddChild(_playlists);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
        };
        row.HorizontalExpand = true;
        body.AddChild(row);
        body.AddChild(_playlistColumn);

        var info = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 4, 8, 0),
        };
        info.AddChild(_listeners);
        info.AddChild(_interference);

        root.AddChild(_status);
        root.AddChild(info);
        root.AddChild(body);
        ContentsContainer.AddChild(root);
    }

    public void UpdateState(StationRadioMixerState state)
    {
        _status.Visible = !state.Connected;
        _listeners.Text = state.Connected ? Loc.GetString("station-radio-mixer-listeners", ("count", state.Listeners)) : string.Empty;
        _interference.Visible = state.Connected && state.Interference > 0f;
        _voice.Fader.Disabled = !state.Connected;
        _master.Fader.Disabled = !state.Connected;
        _voice.SetValue(state.VoiceGain);
        _master.SetValue(state.MasterGain);

        _monitor.Visible = state.MonitorGain != null;
        if (state.MonitorGain is { } monitor)
            _monitor.SetValue(monitor);

        var seen = new HashSet<NetEntity>();
        foreach (var channel in state.Channels)
        {
            seen.Add(channel.Source);
            if (!_sourceStrips.TryGetValue(channel.Source, out var strip))
            {
                var source = channel.Source;
                strip = new ChannelStrip(channel.Name, StripColor, true);
                strip.OnTransport += action => OnTransport?.Invoke(source, action, 0);
                strip.Fader.OnValueChanged += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Source, source, f.Value, false);
                strip.Fader.OnReleased += f => OnFaderChanged?.Invoke(StationRadioMixerFader.Source, source, f.Value, true);
                _sourceStrips[source] = strip;
            }

            strip.SetName(channel.Name);
            strip.SetValue(channel.Gain);
            strip.SetPlayback(channel);
        }

        _playlists.RemoveAllChildren();
        foreach (var channel in state.Channels)
        {
            if (channel.Slots.Count > 1)
                _playlists.AddChild(BuildPlaylist(channel));
        }

        var hasPlaylists = _playlists.ChildCount > 0;
        _playlistColumn.Visible = hasPlaylists;

        foreach (var source in _sourceStrips.Keys.ToArray())
        {
            if (!seen.Contains(source))
                _sourceStrips.Remove(source);
        }

        var ordered = state.Channels.Select(c => (Control) _sourceStrips[c.Source]).ToList();
        if (!_sources.Children.SequenceEqual(ordered))
        {
            _sources.RemoveAllChildren();
            foreach (var strip in ordered)
                _sources.AddChild(strip);
        }

        var wanted = WindowWidth(ordered.Count, hasPlaylists);
        if (Width < wanted)
            SetWidth = wanted;
        if (Height < WindowHeight)
            SetHeight = WindowHeight;
    }

    private Control BuildPlaylist(StationRadioMixerChannel channel)
    {
        var source = channel.Source;
        var list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 2,
        };

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        header.AddChild(new Label
        {
            Text = Loc.GetString("station-radio-mixer-tracks", ("name", channel.Name)),
            HorizontalExpand = true,
        });
        var next = new Button
        {
            Text = Loc.GetString("station-radio-mixer-next"),
            StyleClasses = { StyleClass.ButtonOpenBoth },
            Disabled = channel.Slots.Count(d => d != null) < 2,
        };
        next.OnPressed += _ => OnTransport?.Invoke(source, StationRadioMixerTransport.Next, 0);
        header.AddChild(next);
        list.AddChild(header);

        var dial = new PixelDialProgress { HorizontalAlignment = HAlignment.Center };
        dial.SetPlayback(channel.Playing, channel.Length, channel.Position, channel.PlaybackStart);
        var time = new DialTimeLabel(dial)
        {
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = DiscColor,
            StyleClasses = { StyleStarlight.StyleClassLabelSmall },
        };
        list.AddChild(dial);
        list.AddChild(time);

        for (var i = 0; i < channel.Slots.Count; i++)
        {
            var slot = i;
            var disc = channel.Slots[i];
            var button = new Button
            {
                Text = Loc.GetString("station-radio-mixer-track",
                    ("number", i + 1),
                    ("disc", disc ?? Loc.GetString("station-radio-mixer-empty"))),
                ClipText = true,
                HorizontalExpand = true,
                Disabled = disc == null,
                StyleClasses = { StyleClass.ButtonOpenBoth },
            };
            if (i == channel.CurrentSlot && disc != null)
                button.Label.FontColorOverride = CurrentTrackColor;

            button.OnPressed += _ => OnTransport?.Invoke(source, StationRadioMixerTransport.Select, slot);

            var (eject, _) = ChannelStrip.IconButton(EjectIcon);
            eject.Disabled = disc == null;
            eject.ToolTip = Loc.GetString("station-radio-mixer-eject");
            eject.OnPressed += _ => OnTransport?.Invoke(source, StationRadioMixerTransport.Eject, slot);

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 2,
            };
            row.AddChild(button);
            row.AddChild(eject);
            list.AddChild(row);
        }

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = StripColor,
                BorderColor = StripBorder,
                BorderThickness = new Thickness(1),
            },
        };
        panel.AddChild(list);
        return panel;
    }

    private const float StripWidth = 102;
    private const float PlaylistWidth = 230;
    private const float WindowHeight = 580;

    private static float WindowWidth(int sources, bool playlists) =>
        40 + (sources + 3) * StripWidth + (playlists ? PlaylistWidth + 8 : 0);

    private sealed class DialTimeLabel(PixelDialProgress dial) : Label
    {
        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            Text = dial.Length > TimeSpan.Zero
                ? $"{FormatTime(dial.Elapsed)} / {FormatTime(dial.Length)}"
                : string.Empty;
        }

        private static string FormatTime(TimeSpan time) => $"{(int) time.TotalMinutes}:{time.Seconds:00}";
    }

    private sealed class ChannelStrip : PanelContainer
    {
        public readonly PixelFader Fader = new();

        private const int NameLineLength = 11;

        private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(1.5);

        private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();

        private readonly Label _name;
        private readonly Label _level;
        private readonly PanelContainer? _led;
        private readonly Label? _disc;
        private readonly Button? _playButton;
        private readonly TextureRect? _playIcon;
        private readonly Button? _rewindButton;
        private readonly Button? _nextButton;
        private readonly PixelProgressBar? _progress;
        private readonly Label? _time;

        private bool _playing;
        private TimeSpan _length;
        private TimeSpan _position;
        private TimeSpan _playbackStart;

        public event Action<StationRadioMixerTransport>? OnTransport;

        private float? _pending;
        private TimeSpan _pendingSince;

        public ChannelStrip(string name, Color background, bool isSource)
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = background,
                BorderColor = StripBorder,
                BorderThickness = new Thickness(1),
            };

            var box = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(4),
                SeparationOverride = 4,
            };

            var hidden = isSource ? Color.White : Color.Transparent;

            _led = new PanelContainer
            {
                MinSize = new Vector2(10, 6),
                HorizontalAlignment = HAlignment.Center,
                PanelOverride = new StyleBoxFlat { BackgroundColor = LedOff },
                Modulate = hidden,
            };
            box.AddChild(_led);

            _name = new Label
            {
                ClipText = true,
                Align = Label.AlignMode.Center,
                MinWidth = 76,
                MaxWidth = 76,
                StyleClasses = { StyleStarlight.StyleClassLabelSmall },
            };
            SetName(name);
            box.AddChild(_name);

            _disc = new Label
            {
                ClipText = true,
                Align = Label.AlignMode.Center,
                MinWidth = 76,
                MaxWidth = 76,
                FontColorOverride = DiscColor,
                StyleClasses = { StyleStarlight.StyleClassLabelSmall },
                Modulate = hidden,
            };
            box.AddChild(_disc);

            _progress = new PixelProgressBar { Modulate = hidden };
            box.AddChild(_progress);

            _time = new Label
            {
                Align = Label.AlignMode.Center,
                FontColorOverride = DiscColor,
                StyleClasses = { StyleStarlight.StyleClassLabelSmall },
                Modulate = hidden,
            };
            box.AddChild(_time);

            Fader.HorizontalAlignment = HAlignment.Center;
            Fader.VerticalExpand = true;
            Fader.OnValueChanged += f =>
            {
                _pending = f.Value;
                _pendingSince = _timing.RealTime;
                UpdateLevel(f.Value);
            };
            box.AddChild(Fader);

            _level = new Label { Align = Label.AlignMode.Center };
            box.AddChild(_level);

            var buttons = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalAlignment = HAlignment.Center,
                SeparationOverride = 4,
                Modulate = hidden,
            };
            (_rewindButton, _) = IconButton(RewindIcon);
            (_playButton, _playIcon) = IconButton(PlayIcon);
            (_nextButton, _) = IconButton(NextIcon);
            _rewindButton.OnPressed += _ => OnTransport?.Invoke(StationRadioMixerTransport.Rewind);
            _playButton.OnPressed += _ => OnTransport?.Invoke(StationRadioMixerTransport.TogglePlay);
            _nextButton.OnPressed += _ => OnTransport?.Invoke(StationRadioMixerTransport.Next);
            _rewindButton.Disabled = _playButton.Disabled = _nextButton.Disabled = true;
            _nextButton.Visible = false;
            buttons.AddChild(_rewindButton);
            buttons.AddChild(_playButton);
            buttons.AddChild(_nextButton);
            box.AddChild(buttons);

            AddChild(box);
            UpdateLevel(Fader.Value);
        }

        public void SetName(string name)
        {
            _name.Text = WrapName(name);
            _name.ToolTip = name;
        }

        public void SetValue(float value)
        {
            if (Fader.Grabbed)
                return;

            if (_pending is { } pending)
            {
                var confirmed = MathF.Abs(pending - value) < 0.005f;
                if (!confirmed && _timing.RealTime - _pendingSince < PendingTimeout)
                    return;

                _pending = null;
            }

            Fader.Value = value;
            UpdateLevel(value);
        }

        public void SetPlayback(StationRadioMixerChannel channel)
        {
            var playing = channel.Playing;
            var disc = channel.Disc;
            _playing = playing;
            _length = channel.Length;
            _position = channel.Position;
            _playbackStart = channel.PlaybackStart;

            if (_nextButton != null)
            {
                _nextButton.Visible = channel.Slots.Count > 1;
                _nextButton.Disabled = channel.Slots.Count(d => d != null) < 2;
                _nextButton.ToolTip = Loc.GetString("station-radio-mixer-next");
            }

            if (_led?.PanelOverride is StyleBoxFlat box)
                box.BackgroundColor = playing ? LedOn : disc != null ? LedCued : LedOff;

            if (_disc != null)
            {
                _disc.Text = disc ?? Loc.GetString("station-radio-mixer-empty");
                _disc.ToolTip = disc;
            }

            if (_playButton != null && _rewindButton != null && _playIcon != null)
            {
                _playButton.Disabled = _rewindButton.Disabled = disc == null;
                _playIcon.Texture = IoCManager.Resolve<IResourceCache>().GetTexture(playing ? PauseIcon : PlayIcon);
                _playButton.ToolTip = Loc.GetString(playing ? "vinyl-player-verb-pause" : "vinyl-player-verb-play");
            }
        }

        public static (Button, TextureRect) IconButton(string path)
        {
            var icon = new TextureRect
            {
                Texture = IoCManager.Resolve<IResourceCache>().GetTexture(path),
                TextureScale = new Vector2(2, 2),
                Stretch = TextureRect.StretchMode.KeepCentered,
            };
            var button = new Button
            {
                MinSize = new Vector2(24, 22),
                StyleClasses = { StyleClass.ButtonOpenBoth },
            };
            button.AddChild(icon);
            return (button, icon);
        }

        private static string WrapName(string name)
        {
            var lines = new List<string>();
            lines.Add(string.Empty);
            foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var line = lines[^1];
                if (line.Length == 0 || line.Length + 1 + word.Length <= NameLineLength || lines.Count == 2)
                    lines[^1] = line.Length == 0 ? word : $"{line} {word}";
                else
                    lines.Add(word);
            }

            if (lines.Count == 1)
                lines.Add(string.Empty);

            return string.Join('\n', lines);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_progress == null || _time == null)
                return;

            var position = _playing ? _timing.CurTime - _playbackStart : _position;
            if (position > _length)
                position = _length;
            if (position < TimeSpan.Zero)
                position = TimeSpan.Zero;

            _progress.Paused = !_playing;
            _progress.Progress = _length > TimeSpan.Zero ? (float) (position / _length) : 0f;
            _time.Text = _length > TimeSpan.Zero
                ? $"{FormatTime(position)} / {FormatTime(_length)}"
                : string.Empty;
        }

        private static string FormatTime(TimeSpan time) => $"{(int) time.TotalMinutes}:{time.Seconds:00}";

        private void UpdateLevel(float value) =>
            _level.Text = Loc.GetString("station-radio-mixer-level", ("level", (int) MathF.Round(value * 100)));
    }
}
