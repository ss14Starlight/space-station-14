// Sol
using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Emotes;
using Content.Shared._Starlight.CloudEmotes;
using Content.Shared.Chat.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Sol.UserInterface.Systems.Emotes.Widgets;

/// <summary>
/// Emote button grid used as a tab inside <see cref="SeparatedChatSidePanel"/>.
/// </summary>
public sealed partial class EmotesPanel : Control
{
    private const int ButtonSize = 40;
    private const int IconSize = 29;

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntitySystemManager _entitySystems = default!;

    private EmotesUIController? _emotes;
    private SpriteSystem? _sprite;
    private readonly ScrollContainer _scroll;
    private readonly BoxContainer _content;
    private bool _dirty = true;

    public EmotesPanel()
    {
        IoCManager.InjectDependencies(this);

        HorizontalExpand = true;
        VerticalExpand = true;

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(2, 4),
        };
        _scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        _scroll.AddChild(_content);
        AddChild(_scroll);
    }

    private EmotesUIController Emotes =>
        _emotes ??= UserInterfaceManager.GetUIController<EmotesUIController>();

    private SpriteSystem? Sprite =>
        _sprite ??= _entitySystems.GetEntitySystemOrNull<SpriteSystem>();

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _player.LocalPlayerAttached += OnPlayerChanged;
        _player.LocalPlayerDetached += OnPlayerDetached;
        _dirty = true;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _player.LocalPlayerAttached -= OnPlayerChanged;
        _player.LocalPlayerDetached -= OnPlayerDetached;
    }

    public void Refresh()
    {
        _dirty = true;
        RebuildIfNeeded();
    }

    private void OnPlayerChanged(EntityUid _)
    {
        _dirty = true;
        if (Visible)
            RebuildIfNeeded();
    }

    private void OnPlayerDetached(EntityUid _)
    {
        _dirty = true;
        if (Visible)
            RebuildIfNeeded();
    }

    private void RebuildIfNeeded()
    {
        if (!_dirty)
            return;

        Rebuild();
        _dirty = false;
    }

    private void Rebuild()
    {
        _content.RemoveAllChildren();

        var emotes = Emotes;
        var byCategory = emotes.EnumerateAvailableEmotes()
            .GroupBy(e => e.Category)
            .OrderBy(g => g.Key.ToString());

        foreach (var group in byCategory)
        {
            AddCategorySection(
                emotes.GetCategoryTooltip(group.Key),
                group.OrderBy(e => Loc.GetString(e.Name)),
                emote => CreateEmoteButton(
                    Loc.GetString(emote.Name),
                    emote.Icon,
                    () => emotes.PlayEmote(emote)));
        }

        var cloudEmotes = emotes.EnumerateAvailableCloudEmotes().OrderBy(e => e.ID).ToList();
        if (cloudEmotes.Count > 0)
        {
            AddCategorySection(
                emotes.GetCategoryTooltip(EmoteCategory.Cloud),
                cloudEmotes,
                emote => CreateEmoteButton(
                    emote.ID,
                    emote.Icon,
                    () => emotes.PlayCloudEmote(emote)));
        }

        if (_content.ChildCount == 0)
        {
            _content.AddChild(new Label
            {
                Text = Loc.GetString("separated-chat-side-emotes-empty"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 4),
            });
        }

        // WrapContainer sizes itself to the available width.
    }

    private void AddCategorySection<T>(string title, IEnumerable<T> items, Func<T, Control> createButton)
    {
        var buttons = items.Select(createButton).ToList();
        if (buttons.Count == 0)
            return;

        _content.AddChild(new Label
        {
            Text = title,
            StyleClasses = { StyleClass.LabelHeading },
            Margin = new Thickness(0, 2, 0, 0),
        });

        var wrap = new WrapContainer
        {
            HorizontalExpand = true,
            SeparationOverride = 2,
            CrossSeparationOverride = 2,
        };

        foreach (var button in buttons)
            wrap.AddChild(button);

        _content.AddChild(wrap);
    }

    private Button CreateEmoteButton(string label, SpriteSpecifier icon, Action onPressed)
    {
        Texture? texture = null;
        if (Sprite != null)
        {
            try
            {
                texture = Sprite.Frame0(icon);
            }
            catch
            {
                // Fall back to a text button if the icon can't be loaded.
            }
        }

        var button = new Button
        {
            ToolTip = label,
            StyleClasses = { StyleClass.ButtonOpenBoth },
            Margin = new Thickness(1),
            MinSize = new Vector2(ButtonSize, ButtonSize),
            MaxSize = new Vector2(ButtonSize, ButtonSize),
            SetSize = new Vector2(ButtonSize, ButtonSize),
        };

        if (texture != null)
        {
            button.AddChild(new TextureRect
            {
                Texture = texture,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(IconSize, IconSize),
                MaxSize = new Vector2(IconSize, IconSize),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            });
        }
        else
        {
            button.Text = label;
        }

        button.OnPressed += _ => onPressed();
        return button;
    }
}
