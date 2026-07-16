// Sol
using System.Linq;
using System.Text;
using Content.Client._Starlight.Language.Systems;
using Content.Client.GameTicking.Managers;
using Content.Client.Stylesheets;
using Content.Shared._Starlight.Language;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Sol.UserInterface.Systems.Emotes.Widgets;

/// <summary>
/// Collapsible tabbed side panel above separated-view chat (emotes, round info, languages).
/// </summary>
public sealed partial class SeparatedChatSidePanel : Control
{
    public enum SideTab : byte
    {
        Emotes = 0,
        Info = 1,
        Languages = 2,
    }

    private const float InfoUpdateInterval = 1f;
    private const float CollapsedHeaderHeight = 32f;

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntitySystemManager _entitySystems = default!;

    private readonly PanelContainer _outline;
    private readonly Button _collapseButton;
    private readonly Label _collapsedTitle;
    private readonly BoxContainer _tabHeader;
    private readonly Button[] _tabButtons = new Button[3];
    private readonly Control _body;
    private readonly EmotesPanel _emotesTab;
    private readonly Control _infoTab;
    private readonly Control _languagesTab;
    private readonly Label _roundTimeLabel;
    private readonly Label _mapNameLabel;
    private readonly Label _playerCountLabel;
    private readonly BoxContainer _languagesList;
    private readonly Label _currentLanguageLabel;

    private ClientGameTicker? _ticker;
    private LanguageSystem? _languages;
    private float _infoAccum;
    private bool _languagesDirty = true;
    private bool _expanded;
    private SideTab _currentTab = SideTab.Emotes;

    /// <summary>Fired when the panel is collapsed or expanded.</summary>
    public event Action<bool>? OnExpansionChanged;

    public bool IsExpanded => _expanded;

    public SeparatedChatSidePanel()
    {
        IoCManager.InjectDependencies(this);

        HorizontalExpand = true;
        VerticalExpand = true;
        MinHeight = CollapsedHeaderHeight;

        _outline = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        AddChild(_outline);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 0,
        };
        _outline.AddChild(root);

        // --- Header ---
        var header = new PanelContainer
        {
            HorizontalExpand = true,
            MinHeight = CollapsedHeaderHeight,
        };
        root.AddChild(header);

        var headerRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(2, 2),
        };
        header.AddChild(headerRow);

        _collapseButton = new Button
        {
            Text = "▶",
            MinWidth = 28,
            MaxWidth = 28,
            MinHeight = 26,
            StyleClasses = { StyleClass.ButtonOpenBoth },
            ToolTip = Loc.GetString("separated-chat-side-expand-tooltip"),
        };
        _collapseButton.OnPressed += _ => SetExpanded(!_expanded);
        headerRow.AddChild(_collapseButton);

        _collapsedTitle = new Label
        {
            Text = Loc.GetString("separated-chat-side-collapsed-title"),
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Visible = true,
        };
        headerRow.AddChild(_collapsedTitle);

        _tabHeader = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 2,
            Visible = false,
        };
        headerRow.AddChild(_tabHeader);

        for (var i = 0; i < _tabButtons.Length; i++)
        {
            var tab = (SideTab)i;
            var button = new Button
            {
                Text = GetTabTitle(tab),
                ToggleMode = true,
                Pressed = tab == _currentTab,
                StyleClasses = { StyleClass.ButtonOpenBoth },
                HorizontalExpand = true,
            };
            button.OnPressed += _ => SelectTab(tab);
            _tabButtons[i] = button;
            _tabHeader.AddChild(button);
        }

        // --- Body ---
        _body = new Control
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
            MinHeight = 0,
        };
        root.AddChild(_body);

        var bodyLayout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _body.AddChild(bodyLayout);

        // Emotes tab
        _emotesTab = new EmotesPanel();
        bodyLayout.AddChild(_emotesTab);

        // Info tab
        _infoTab = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(6),
            Visible = false,
        };
        _roundTimeLabel = AddInfoRow((BoxContainer)_infoTab, "separated-chat-side-info-round-time");
        _mapNameLabel = AddInfoRow((BoxContainer)_infoTab, "separated-chat-side-info-map");
        _playerCountLabel = AddInfoRow((BoxContainer)_infoTab, "separated-chat-side-info-players");
        bodyLayout.AddChild(_infoTab);

        // Languages tab
        _languagesTab = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(6),
            Visible = false,
        };
        _currentLanguageLabel = new Label
        {
            HorizontalExpand = true,
            StyleClasses = { StyleClass.LabelHeading },
        };
        _languagesTab.AddChild(_currentLanguageLabel);

        var langScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        _languagesList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        langScroll.AddChild(_languagesList);
        _languagesTab.AddChild(langScroll);
        bodyLayout.AddChild(_languagesTab);

        // Screen applies remembered expand preference after construction.
        SetExpanded(false, notify: false);
    }

    /// <summary>Expanded split height used when the player has not resized manually.</summary>
    public const float DefaultExpandedSplitHeight = 220f;

    /// <summary>Apply expand/collapse without necessarily notifying listeners.</summary>
    public void SetExpandedState(bool expanded, bool notify = true)
    {
        SetExpanded(expanded, notify);
    }

    private static string GetTabTitle(SideTab tab) => tab switch
    {
        SideTab.Emotes => Loc.GetString("separated-chat-side-tab-emotes"),
        SideTab.Info => Loc.GetString("separated-chat-side-tab-info"),
        SideTab.Languages => Loc.GetString("separated-chat-side-tab-languages"),
        _ => string.Empty,
    };

    private static Label AddInfoRow(BoxContainer parent, string locKey)
    {
        var label = new Label
        {
            Text = Loc.GetString(locKey, ("value", "—")),
            HorizontalExpand = true,
        };
        parent.AddChild(label);
        return label;
    }

    private void SetExpanded(bool expanded, bool notify = true)
    {
        _expanded = expanded;

        _collapseButton.Text = expanded ? "▼" : "▶";
        _collapseButton.ToolTip = Loc.GetString(expanded
            ? "separated-chat-side-collapse-tooltip"
            : "separated-chat-side-expand-tooltip");

        _collapsedTitle.Visible = !expanded;
        _tabHeader.Visible = expanded;
        _body.Visible = expanded;

        // Keep border styling in both states so expand/collapse does not shift layout.
        _outline.StyleClasses.Clear();
        _outline.StyleClasses.Add("PdaBorderRect");

        if (expanded)
        {
            SelectTab(_currentTab);
        }

        if (notify)
            OnExpansionChanged?.Invoke(expanded);
    }

    private void SelectTab(SideTab tab)
    {
        _currentTab = tab;

        for (var i = 0; i < _tabButtons.Length; i++)
            _tabButtons[i].Pressed = (SideTab)i == tab;

        _emotesTab.Visible = tab == SideTab.Emotes;
        _infoTab.Visible = tab == SideTab.Info;
        _languagesTab.Visible = tab == SideTab.Languages;

        RefreshActiveTab();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _ticker ??= _entitySystems.GetEntitySystem<ClientGameTicker>();
        _languages ??= _entitySystems.GetEntitySystem<LanguageSystem>();
        _languages.OnLanguagesChanged += OnLanguagesChanged;
        _player.LocalPlayerAttached += OnPlayerAttached;
        _player.LocalPlayerDetached += OnPlayerDetached;

        _languagesDirty = true;
        if (_expanded)
            RefreshActiveTab();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        if (_languages != null)
            _languages.OnLanguagesChanged -= OnLanguagesChanged;

        _player.LocalPlayerAttached -= OnPlayerAttached;
        _player.LocalPlayerDetached -= OnPlayerDetached;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_expanded || _currentTab != SideTab.Info)
            return;

        _infoAccum += args.DeltaSeconds;
        if (_infoAccum < InfoUpdateInterval)
            return;

        _infoAccum = 0f;
        UpdateInfoTab();
    }

    private void RefreshActiveTab()
    {
        if (!_expanded || !IsInsideTree)
            return;

        switch (_currentTab)
        {
            case SideTab.Emotes:
                _emotesTab.Refresh();
                break;
            case SideTab.Info:
                UpdateInfoTab();
                break;
            case SideTab.Languages:
                if (_languagesDirty)
                    RebuildLanguages();
                break;
        }
    }

    private void OnLanguagesChanged()
    {
        _languagesDirty = true;
        if (_expanded && _currentTab == SideTab.Languages)
            RebuildLanguages();
    }

    private void OnPlayerAttached(EntityUid _)
    {
        _languagesDirty = true;
        RefreshActiveTab();
    }

    private void OnPlayerDetached(EntityUid _)
    {
        _languagesDirty = true;
        RefreshActiveTab();
    }

    private void UpdateInfoTab()
    {
        _ticker ??= _entitySystems.GetEntitySystemOrNull<ClientGameTicker>();

        var roundTime = _ticker?.RoundDuration() ?? TimeSpan.Zero;
        _roundTimeLabel.Text = Loc.GetString(
            "separated-chat-side-info-round-time",
            ("value", roundTime.ToString(@"hh\:mm\:ss")));

        _mapNameLabel.Text = Loc.GetString(
            "separated-chat-side-info-map",
            ("value", GetMapName()));

        _playerCountLabel.Text = Loc.GetString(
            "separated-chat-side-info-players",
            ("value", _player.PlayerCount.ToString()));
    }

    private string GetMapName()
    {
        if (_ticker is { StationNames.Count: > 0 })
        {
            var builder = new StringBuilder();
            foreach (var name in _ticker.StationNames.Values.OrderBy(n => n))
            {
                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(name);
            }

            return builder.ToString();
        }

        return Loc.GetString("separated-chat-side-info-map-unknown");
    }

    private void RebuildLanguages()
    {
        _languagesDirty = false;
        _languagesList.RemoveAllChildren();

        _languages ??= _entitySystems.GetEntitySystemOrNull<LanguageSystem>();
        var speaker = _languages?.GetLocalSpeaker();
        if (speaker == null || _languages == null)
        {
            _currentLanguageLabel.Text = Loc.GetString("separated-chat-side-languages-none");
            _languagesList.AddChild(new Label
            {
                Text = Loc.GetString("separated-chat-side-languages-unavailable"),
                HorizontalAlignment = HAlignment.Center,
            });
            return;
        }

        var current = speaker.CurrentLanguage;
        var currentName = Loc.GetString($"language-{current}-name");
        _currentLanguageLabel.Text = Loc.GetString(
            "separated-chat-side-languages-current",
            ("language", currentName));

        foreach (var language in speaker.SpokenLanguages)
        {
            var proto = _languages.GetLanguagePrototype(language);
            var name = proto?.Name ?? Loc.GetString($"language-{language}-name");
            var isCurrent = language == current;

            var button = new Button
            {
                Text = name,
                HorizontalExpand = true,
                Disabled = isCurrent,
                StyleClasses = { StyleClass.ButtonOpenBoth },
                ToolTip = proto?.Description,
            };

            var captured = language;
            button.OnPressed += _ =>
            {
                _languages.RequestSetLanguage(captured);
                _languagesDirty = true;
                RebuildLanguages();
            };

            _languagesList.AddChild(button);
        }

        if (_languagesList.ChildCount == 0)
        {
            _languagesList.AddChild(new Label
            {
                Text = Loc.GetString("separated-chat-side-languages-empty"),
                HorizontalAlignment = HAlignment.Center,
            });
        }
    }
}
