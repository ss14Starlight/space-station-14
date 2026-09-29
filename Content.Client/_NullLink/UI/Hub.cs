using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared._NullLink;
using Content.Shared._Starlight.Abstract.Conditions;
using Content.Shared._Starlight.GhostTheme;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._NullLink.UI;

internal sealed partial class Hub : PanelContainer, IDisposable
{
    private const string IconFont = "/Fonts/_NullLink/GameIcons/game-icons.ttf";
    private const string TextFont = "/Fonts/NotoSans/NotoSans-Regular.ttf";
    private const int TextSize = 11;

    private const string RecognitionGlyph = "\uEC45"; // playtime recognition (clock)
    private const string GhostGlyph = "\uEE0F"; // ghost theme
    private const string BunkerGlyph = "\uE992"; // panic bunker
    private const string ConnectGlyph = "\uEA03"; // play / connect

    private static readonly Color AccentColor = Color.FromHex("#9b59b6");
    private static readonly Color HeaderColor = Color.FromHex("#2b2b33");
    private static readonly Color RowColor = Color.FromHex("#20202a");
    private static readonly Color PanelColor = Color.FromHex("#14141a");
    private static readonly Color WarningColor = Color.FromHex("#e74c3c");
    private static readonly Color GhostColor = Color.FromHex("#5cb85c");

    [Dependency] private ILogManager _logs = default!;
    [Dependency] private IEntitySystemManager _systemManager = default!;
    [Dependency] private IGameController _game = default!;
    [Dependency] private IResourceCache _resCache = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private HubSystem _hub = default!;
    private ISawmill _sawmill = default!;

    private readonly BoxContainer _root;
    private readonly ScrollContainer _scroll;
    private readonly Control _newsBlock;
    private readonly Button _toggleButton;
    private bool _collapsed;
    private readonly HashSet<string> _expandedProjects = [];

    private HashSet<string>? _ghostThemeServers;

    public Hub()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _logs.GetSawmill("Hub");

        HorizontalAlignment = HAlignment.Left;
        VerticalAlignment = VAlignment.Bottom;
        Margin = new Thickness(4);
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = PanelColor,
            BorderColor = StyleNano.NanoGold,
            BorderThickness = new Thickness(2),
        };

        var outer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            MinWidth = 320,
            MaxWidth = 440,
        };

        var header = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
        header.AddChild(new Label
        {
            Text = Loc.GetString("nulllink-hub-header"),
            FontColorOverride = AccentColor,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });
        _toggleButton = new Button
        {
            StyleBoxOverride = ButtonBox(HeaderColor),
            VerticalAlignment = VAlignment.Center,
        };
        _toggleButton.OnPressed += _ => SetCollapsed(!_collapsed);
        header.AddChild(_toggleButton);
        outer.AddChild(header);

        _newsBlock = new PanelContainer
        {
            Visible = false,
            PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderColor },
            Margin = new Thickness(0, 4, 0, 0),
        };
        outer.AddChild(_newsBlock);

        _scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VScrollEnabled = true,
            ReturnMeasure = true,
            HorizontalExpand = true,
            MaxHeight = 460,
            Margin = new Thickness(0, 4, 0, 0),
        };
        _root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        _scroll.AddChild(_root);
        outer.AddChild(_scroll);

        AddChild(outer);

        SetCollapsed(false);

        _systemManager.SystemLoaded += OnSystemLoaded;
    }

    private void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;
        _scroll.Visible = !collapsed;
        _toggleButton.Text = collapsed ? "►" : "▼";
    }

    private void OnSystemLoaded(object? sender, SystemChangedArgs e)
    {
        if (e.System is not HubSystem hubSystem)
            return;

        _hub = hubSystem;
        _hub.OnInitialized += OnHubChanged;
        _hub.OnRequirementsUpdated += OnHubChanged;
        _hub.OnServerUpdated += OnServerChanged;
        _hub.OnServerInfoUpdated += OnServerInfoChanged;
        _hub.OnServersRemoved += OnServerRemoved;

        _systemManager.SystemLoaded -= OnSystemLoaded;

        if (_hub.HubInitialized)
            Rebuild();
    }

    private void OnHubChanged() => Rebuild();
    private void OnServerChanged(string key, NullLink.Server server) => Rebuild();
    private void OnServerInfoChanged(string key, NullLink.ServerInfo info) => Rebuild();
    private void OnServerRemoved(string key) => Rebuild();

    private void Rebuild()
    {
        _root.RemoveAllChildren();

        var servers = _hub.Servers;
        if (servers is null || servers.Count == 0)
        {
            _root.AddChild(new Label
            {
                Text = Loc.GetString("nulllink-hub-no-servers"),
                FontOverride = _resCache.GetFont(TextFont, TextSize),
                FontColorOverride = Color.Gray,
                Margin = new Thickness(4),
            });
            return;
        }

        if (servers.Count == 1)
        {
            var (key, server) = servers.First();
            _root.AddChild(MakeServerRow(key, server));
            return;
        }

        var groups = new Dictionary<string, List<KeyValuePair<string, NullLink.Server>>>();
        foreach (var kv in servers)
        {
            var project = ProjectOf(kv.Key);
            if (!groups.TryGetValue(project, out var list))
                groups[project] = list = [];
            list.Add(kv);
        }

        var current = _hub.CurrentProject ?? string.Empty;

        foreach (var group in groups)
        {
            if (!string.Equals(group.Key, current, StringComparison.OrdinalIgnoreCase))
                continue;

            AddSectionTitle(group.Key, AccentColor);
            foreach (var (key, server) in OrderServers(group.Value))
                _root.AddChild(MakeServerRow(key, server));
        }

        foreach (var group in groups
                     .Where(g => !string.Equals(g.Key, current, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Value.Count == 1)
            {
                var (soloKey, soloServer) = group.Value[0];
                _root.AddChild(MakeServerRow(soloKey, soloServer));
                continue;
            }

            var expanded = _expandedProjects.Contains(group.Key);
            _root.AddChild(MakeProjectHeader(group.Key, group.Value, expanded));

            if (!expanded)
                continue;

            foreach (var (key, server) in OrderServers(group.Value))
                _root.AddChild(MakeServerRow(key, server));
        }
    }

    private void AddSectionTitle(string project, Color color)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderColor },
            Margin = new Thickness(0, 4, 0, 2),
        };
        panel.AddChild(new Label
        {
            Text = project,
            FontOverride = _resCache.GetFont(TextFont, TextSize),
            FontColorOverride = color,
            Margin = new Thickness(6, 3),
        });
        _root.AddChild(panel);
    }

    private Control MakeProjectHeader(string project, List<KeyValuePair<string, NullLink.Server>> list, bool expanded)
    {
        var onlinePlayers = list.Sum(kv => _hub.ServerInfo?.GetValueOrDefault(kv.Key)?.Players ?? 0);

        var button = new Button
        {
            StyleBoxOverride = ButtonBox(RowColor),
            HorizontalExpand = true,
            Margin = new Thickness(0, 1),
        };

        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        content.AddChild(new Label
        {
            Text = expanded ? "▼" : "►",
            FontOverride = _resCache.GetFont(TextFont, TextSize),
            FontColorOverride = AccentColor,
            Margin = new Thickness(2, 0, 6, 0),
            VerticalAlignment = VAlignment.Center,
        });
        content.AddChild(new Label
        {
            Text = project,
            FontOverride = _resCache.GetFont(TextFont, TextSize),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });

        var dots = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 2,
            Margin = new Thickness(0, 0, 6, 0),
        };
        foreach (var (memberKey, _) in OrderServers(list))
            dots.AddChild(MakeStatusDot(_hub.ServerInfo?.GetValueOrDefault(memberKey)));
        content.AddChild(dots);

        content.AddChild(new Label
        {
            Text = Loc.GetString("nulllink-hub-online", ("online", onlinePlayers)),
            FontOverride = _resCache.GetFont(TextFont, TextSize),
            FontColorOverride = Color.Gray,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        });
        button.AddChild(content);

        button.OnPressed += _ =>
        {
            if (!_expandedProjects.Remove(project))
                _expandedProjects.Add(project);
            Rebuild();
        };

        return button;
    }

    private Control MakeServerRow(string key, NullLink.Server server)
    {
        var info = _hub.ServerInfo?.GetValueOrDefault(key);
        var insufficient = _hub.InsufficientPlaytime.GetValueOrDefault(key);
        var (recognized, ghost) = IconsFor(key);

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = RowColor,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
            },
            Margin = new Thickness(0, 1),
        };

        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };

        row.AddChild(MakeStatusDot(info));

        var title = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            ToolTip = server.Description,
            Margin = new Thickness(6, 0),
        };
        title.SetMessage(FormattedMessage.FromMarkupPermissive($"[font size={TextSize}]{server.Title}[/font]"));
        row.AddChild(title);

        if (info?.PanicBunkerActive == true)
            row.AddChild(MakeGlyph(BunkerGlyph, WarningColor, PanicBunkerTooltip(server)));
        if (recognized)
            row.AddChild(MakeGlyph(RecognitionGlyph, StyleNano.NanoGold, Loc.GetString("nulllink-hub-recognition-tooltip")));
        if (ghost)
            row.AddChild(MakeGlyph(GhostGlyph, GhostColor, Loc.GetString("nulllink-hub-ghost-tooltip")));

        var players = new Label
        {
            Text = $"{info?.Players ?? 0}/{info?.MaxPlayers ?? 0}",
            FontOverride = _resCache.GetFont(TextFont, TextSize),
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(6, 0, 6, 0),
        };
        if (insufficient)
        {
            players.FontColorOverride = WarningColor;
            players.ToolTip = Loc.GetString("nulllink-hub-insufficient-playtime");
        }
        row.AddChild(players);

        row.AddChild(MakeConnectButton(server));

        panel.AddChild(row);
        return panel;
    }

    private Control MakeConnectButton(NullLink.Server server)
    {
        var connected = _hub.CurrentGameHostName == server.Title;
        var button = new Button
        {
            Disabled = connected,
            TooltipDelay = 0.2f,
            ToolTip = Loc.GetString(connected ? "nulllink-hub-connected" : "nulllink-hub-connect"),
            StyleBoxOverride = ButtonBox(connected ? PanelColor : HeaderColor),
            VerticalAlignment = VAlignment.Center,
        };
        button.AddChild(new Label
        {
            Text = ConnectGlyph,
            FontOverride = _resCache.GetFont(IconFont, 16),
            FontColorOverride = connected ? Color.Gray : GhostColor,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        });
        if (!connected)
            button.OnPressed += _ => _game.Redial(server.ConnectionString);
        return button;
    }

    private static StyleBoxFlat ButtonBox(Color color) => new()
    {
        BackgroundColor = color,
        ContentMarginLeftOverride = 6,
        ContentMarginRightOverride = 6,
        ContentMarginTopOverride = 3,
        ContentMarginBottomOverride = 3,
    };

    private static Control MakeStatusDot(NullLink.ServerInfo? info)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = StatusColor(info) },
            MinSize = new Vector2(10, 10),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Stop,
            ToolTip = info?.GetStatus() ?? Loc.GetString("nulllink-hub-status-unknown"),
            TooltipDelay = 0.2f,
        };
    }

    private Control MakeGlyph(string glyph, Color color, string tooltip)
    {
        return new Label
        {
            Text = glyph,
            FontOverride = _resCache.GetFont(IconFont, 16),
            FontColorOverride = color,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Stop,
            ToolTip = tooltip,
            TooltipDelay = 0.2f,
            Margin = new Thickness(2, 0, 0, 0),
        };
    }

    private static string PanicBunkerTooltip(NullLink.Server server)
    {
        var age = server.PanicBunkerMinAccountAge is { } a
            ? Loc.GetString("nulllink-hub-bunker-age", ("minutes", a))
            : Loc.GetString("nulllink-hub-bunker-age-any");
        var playtime = server.PanicBunkerMinOverallMinutes is { } p
            ? Loc.GetString("nulllink-hub-bunker-playtime", ("minutes", p))
            : Loc.GetString("nulllink-hub-bunker-playtime-any");
        return Loc.GetString("nulllink-hub-bunker-tooltip", ("age", age), ("playtime", playtime));
    }

    private static Color StatusColor(NullLink.ServerInfo? info) => info?.Status switch
    {
        NullLink.ServerStatus.Lobby => Color.FromHex("#5bc0de"),
        NullLink.ServerStatus.Round => Color.FromHex("#5cb85c"),
        NullLink.ServerStatus.RoundEnding => Color.FromHex("#f0ad4e"),
        _ => Color.Gray,
    };

    private (bool recognized, bool ghost) IconsFor(string key)
    {
        var recognized = false;
        var project = _hub.CurrentProject;
        var server = _hub.CurrentServer;
        if (!string.IsNullOrEmpty(project)
            && !string.IsNullOrEmpty(server)
            && _proto.TryIndex<ServerPlaytimeRecognitionPrototype>(project.ToUpper(), out var reco)
            && reco.Recognition.TryGetValue(server.ToLower(), out var rec))
            recognized = rec.Contains(key);

        return (recognized, GhostThemeServers().Contains(key));
    }

    private HashSet<string> GhostThemeServers()
    {
        if (_ghostThemeServers is not null)
            return _ghostThemeServers;

        _ghostThemeServers = [];
        foreach (var theme in _proto.EnumeratePrototypes<GhostThemePrototype>())
            foreach (var requirement in theme.Requirements)
                if (requirement is NullLinkPlayTimeRequirement nl && !string.IsNullOrEmpty(nl.Server))
                    _ghostThemeServers.Add(nl.Server);

        return _ghostThemeServers;
    }

    private static string ProjectOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot < 0 ? key : key[..dot];
    }

    private static IEnumerable<KeyValuePair<string, NullLink.Server>> OrderServers(
        List<KeyValuePair<string, NullLink.Server>> list)
        => list.OrderBy(kv => kv.Value.Title, StringComparer.OrdinalIgnoreCase);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _systemManager.SystemLoaded -= OnSystemLoaded;
        if (_hub is null)
            return;

        _hub.OnInitialized -= OnHubChanged;
        _hub.OnRequirementsUpdated -= OnHubChanged;
        _hub.OnServerUpdated -= OnServerChanged;
        _hub.OnServerInfoUpdated -= OnServerInfoChanged;
        _hub.OnServersRemoved -= OnServerRemoved;
    }
}
