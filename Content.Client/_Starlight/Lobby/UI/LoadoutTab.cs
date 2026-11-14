using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// The loadouts of every role in one tab of the character editor, in place of a window per role.
/// On the left a tree: departments fold open to their roles, the picked role opens to its groups.
/// On the right the items of the picked group as cards. The side preview of the editor stays visible.
/// Only shows things; the profile editor applies the changes and keeps the preview dressed.
/// </summary>
/// <remarks>
/// Nothing here is a <see cref="WrapContainer"/> outside a scroll container: one asks for all the width
/// it's offered, which squeezed the side preview away.
/// </remarks>
public sealed class LoadoutTab : BoxContainer
{
    private const float CardWidth = 104;
    private const float CardHeight = 118;

    private readonly IPrototypeManager _prototypes;
    private readonly IEntityManager _entities;
    private readonly int _maxNameLength;

    private readonly Label _title = new() { StyleClasses = { "TraitsTitleLabel" }, HorizontalExpand = true, ClipText = true };
    private readonly Label _points = new() { StyleClasses = { "TraitsStatLabel" } };
    private readonly BoxContainer _tree = new() { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(0, 0, 4, 0) };
    private readonly BoxContainer _nameRow = new() { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8, Visible = false };
    private readonly Label _nameLabel = new() { VAlign = Label.VAlignMode.Center };
    private readonly LineEdit _nameEdit = new() { HorizontalExpand = true };
    private readonly Label _groupTitle = new() { StyleClasses = { "TraitsCategoryNameLabel" }, HorizontalExpand = true, ClipText = true };
    private readonly Label _limits = new() { StyleClasses = { StyleClass.LabelSubText } };
    private readonly LineEdit _search;
    private readonly WrapContainer _items = new() { SeparationOverride = 6, CrossSeparationOverride = 6, Margin = new Thickness(4) };
    private readonly Label _empty = new()
    {
        Text = Loc.GetString("loadout-tab-pick-role"),
        StyleClasses = { StyleClass.LabelSubText },
        HorizontalAlignment = HAlignment.Center,
        VerticalAlignment = VAlignment.Center,
        HorizontalExpand = true,
        VerticalExpand = true,
    };
    private readonly BoxContainer _editor;

    private readonly List<Section> _sections = [];
    private readonly Dictionary<string, (Button Button, BoxContainer Groups, Section Section, string Name)> _roles = [];
    private readonly List<(LoadoutPrototype Proto, ContainerButton Card, string Name)> _cards = [];

    private string? _roleKey;
    private RoleLoadoutPrototype? _roleProto;
    private ProtoId<LoadoutGroupPrototype>? _group;

    public event Action<string>? OnRoleSelected;
    public event Action<string>? OnNameChanged;
    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnLoadoutPressed;
    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnLoadoutUnpressed;

    /// <summary>
    /// A foldable department, or the antags or species, holding its role buttons.
    /// </summary>
    private sealed class Section
    {
        public readonly BoxContainer Roles = new()
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(6, 4, 2, 4),
            Visible = false,
        };
        public readonly Label Arrow = new() { Text = "▶", StyleClasses = { "TraitsCategoryExpandIcon" }, Margin = new Thickness(0, 0, 6, 0) };

        public void SetOpen(bool open)
        {
            Roles.Visible = open;
            Arrow.Text = open ? "▼" : "▶";
        }
    }

    public LoadoutTab()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _entities = IoCManager.Resolve<IEntityManager>();
        _maxNameLength = IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.MaxLoadoutNameLength);

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 8;
        Margin = new Thickness(0, 4, 0, 0);

        AddChild(new PanelContainer
        {
            StyleClasses = { "TraitsHeaderPanel" },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Margin = new Thickness(12, 10),
                    SeparationOverride = 8,
                    Children = { _title, _points },
                },
            },
        });

        _nameEdit.IsValid = text => text.Length <= _maxNameLength;
        _nameEdit.ToolTip = Loc.GetString("loadout-name-edit-tooltip", ("max", _maxNameLength));
        _nameEdit.OnTextChanged += args => OnNameChanged?.Invoke(args.Text);
        _nameRow.AddChild(_nameLabel);
        _nameRow.AddChild(_nameEdit);

        _search = new LineEdit
        {
            PlaceHolder = Loc.GetString("loadout-tab-search"),
            HorizontalExpand = true,
            Margin = new Thickness(10, 8),
            StyleClasses = { "TraitsSearchInput" },
        };
        _search.OnTextChanged += _ => FilterItems();

        _editor = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
            Visible = false,
            Children =
            {
                _nameRow,
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children = { _groupTitle, _limits },
                },
                new PanelContainer { StyleClasses = { "TraitsSearchBar" }, Children = { _search } },
                new PanelContainer
                {
                    StyleClasses = { "InsetPanel" },
                    VerticalExpand = true,
                    Children =
                    {
                        new ScrollContainer
                        {
                            HScrollEnabled = false,
                            VerticalExpand = true,
                            ReserveScrollbarSpace = true,
                            Children = { _items },
                        },
                    },
                },
            },
        };

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            VerticalExpand = true,
            SeparationOverride = 8,
            Children =
            {
                new ScrollContainer
                {
                    HScrollEnabled = false,
                    VerticalExpand = true,
                    SetWidth = 230,
                    Children = { _tree },
                },
                _empty,
                _editor,
            },
        });
    }

    #region Roles

    public void ClearRoles()
    {
        _tree.RemoveAllChildren();
        _sections.Clear();
        _roles.Clear();
    }

    /// <summary>
    /// Starts a foldable section; the roles added after it go in it.
    /// </summary>
    public void AddRoleHeader(string text, Color? color)
    {
        var section = new Section();
        var header = new Button { StyleClasses = { "TraitsCategoryHeaderButton" }, HorizontalExpand = true };
        header.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 5),
            Children =
            {
                section.Arrow,
                new Label { Text = text, StyleClasses = { "TraitsCategoryNameLabel" }, HorizontalExpand = true, ClipText = true },
            },
        });
        header.OnPressed += _ => section.SetOpen(!section.Roles.Visible);

        var category = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(0, 0, 0, 4),
            Children =
            {
                new PanelContainer { StyleClasses = { "TraitsCategoryHeader" }, Children = { header } },
            },
        };
        if (color != null)
            category.AddChild(new PanelContainer { PanelOverride = new StyleBoxFlat(color.Value), SetHeight = 2 });
        category.AddChild(new PanelContainer { StyleClasses = { "TraitsCategoryContent" }, Children = { section.Roles } });

        _sections.Add(section);
        _tree.AddChild(category);
    }

    public void AddRole(string key, string name, Texture? icon)
    {
        if (_sections.Count == 0)
            AddRoleHeader(string.Empty, null);
        var section = _sections[^1];

        var button = new Button { ToggleMode = true, HorizontalExpand = true, ToolTip = name };
        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
        if (icon != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = icon,
                TextureScale = new Vector2(2, 2),
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepCentered,
            });
        }
        row.AddChild(new Label { Text = name, ClipText = true, HorizontalExpand = true, VAlign = Label.VAlignMode.Center });
        button.AddChild(row);
        button.OnPressed += _ => OnRoleSelected?.Invoke(key);

        // The groups of the picked role open under it.
        var groups = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(14, 2, 0, 4),
            Visible = false,
        };

        _roles[key] = (button, groups, section, name);
        section.Roles.AddChild(button);
        section.Roles.AddChild(groups);
    }

    /// <summary>
    /// Lights the role being edited and folds its section open. Set by hand: a button group forgets itself when the window is reopened.
    /// </summary>
    public void HighlightRole(string? key)
    {
        _roleKey = key;
        foreach (var (roleKey, (button, groups, section, _)) in _roles)
        {
            var current = roleKey == key;
            button.Pressed = current;
            groups.Visible = current;
            if (current)
                section.SetOpen(true);
        }
    }

    #endregion

    #region Loadout

    /// <summary>
    /// Shows the loadout of a role, starting on its first group.
    /// </summary>
    public void ShowLoadout(string title, HumanoidCharacterProfile profile, RoleLoadout loadout, RoleLoadoutPrototype proto, ICommonSession session)
    {
        _title.Text = title;
        _empty.Visible = false;
        _editor.Visible = true;

        if (_roleProto?.ID != proto.ID)
        {
            _roleProto = proto;
            _group = VisibleGroups(proto).Select(g => (ProtoId<LoadoutGroupPrototype>?) g.ID).FirstOrDefault();
            _search.Text = string.Empty;
            BuildCards();
        }

        _nameRow.Visible = proto.CanCustomizeName;
        if (proto.CanCustomizeName)
        {
            _nameLabel.Text = Loc.GetString(proto.NameDataset == null ? "loadout-name-edit-label" : "loadout-name-edit-label-dataset");
            if (!_nameEdit.HasKeyboardFocus())
                _nameEdit.Text = loadout.EntityName ?? string.Empty;
        }

        Refresh(profile, loadout, session);
    }

    /// <summary>
    /// Nothing picked: asks to pick a role.
    /// </summary>
    public void ShowNothing()
    {
        _roleProto = null;
        _group = null;
        _title.Text = Loc.GetString("loadout-tab-title");
        _points.Text = string.Empty;
        _empty.Visible = true;
        _editor.Visible = false;
        _items.RemoveAllChildren();
        _cards.Clear();
    }

    private IEnumerable<LoadoutGroupPrototype> VisibleGroups(RoleLoadoutPrototype proto)
    {
        foreach (var id in proto.Groups)
        {
            if (_prototypes.TryIndex(id, out var group) && !group.Hidden)
                yield return group;
        }
    }

    /// <summary>
    /// Group names repeat the role ("Captain head"); under the role in the tree only the rest is needed.
    /// </summary>
    private string GroupName(LoadoutGroupPrototype group)
    {
        var name = Loc.GetString(group.Name);
        if (_roleKey != null && _roles.TryGetValue(_roleKey, out var role)
            && name.Length > role.Name.Length + 1
            && name.StartsWith(role.Name + " ", StringComparison.CurrentCultureIgnoreCase))
        {
            // Substring and ToUpperInvariant, not char + string: that concat builds a span over the char, which the sandbox forbids.
            name = name[(role.Name.Length + 1)..];
            name = string.Concat(name[..1].ToUpperInvariant(), name.AsSpan(1));
        }

        return name;
    }

    /// <summary>
    /// Updates what's picked and allowed without rebuilding the cards, so their sprites aren't spawned again.
    /// </summary>
    public void Refresh(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession session)
    {
        if (_roleProto == null)
            return;

        _points.Text = _roleProto.Points != null && loadout.Points != null
            ? Loc.GetString("loadouts-points-limit", ("count", loadout.Points.Value), ("max", _roleProto.Points.Value))
            : string.Empty;

        RefreshGroups(profile, loadout, session);

        if (_group is not { } groupId || !_prototypes.TryIndex(groupId, out var groupProto))
        {
            _groupTitle.Text = string.Empty;
            _limits.Text = string.Empty;
            return;
        }

        _groupTitle.Text = GroupName(groupProto);
        var limits = new List<string>();
        if (groupProto.MinLimit > 0)
            limits.Add(Loc.GetString("loadouts-min-limit", ("count", groupProto.MinLimit)));
        if (groupProto.MaxLimit > 0)
            limits.Add(Loc.GetString("loadouts-max-limit", ("count", groupProto.MaxLimit)));
        _limits.Text = string.Join("   ", limits);

        var selected = loadout.SelectedLoadouts.TryGetValue(groupId, out var list) ? list : null;
        var collection = IoCManager.Instance!;
        foreach (var (proto, card, name) in _cards)
        {
            var valid = loadout.IsValid(profile, session, proto.ID, collection, out var reason);
            card.Disabled = !valid;
            if (selected?.Any(l => l.Prototype == proto.ID) == true)
                card.AddStyleClass(MarkingCard.StyleClassPicked);
            else
                card.RemoveStyleClass(MarkingCard.StyleClassPicked);

            var tooltip = new FormattedMessage();
            tooltip.AddText(name);
            if (Description(proto) is { Length: > 0 } description)
            {
                tooltip.PushNewline();
                tooltip.AddText(description);
            }
            if (!reason.IsEmpty)
            {
                tooltip.PushNewline();
                tooltip.AddMessage(reason);
            }
            card.TooltipSupplier = _ =>
            {
                var t = new Tooltip();
                t.SetMessage(tooltip);
                return t;
            };
        }
    }

    /// <summary>
    /// The group buttons under the picked role in the tree, with how many items each has picked.
    /// </summary>
    private void RefreshGroups(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession session)
    {
        if (_roleProto == null || _roleKey == null || !_roles.TryGetValue(_roleKey, out var role))
            return;

        role.Groups.RemoveAllChildren();
        foreach (var group in VisibleGroups(_roleProto))
        {
            var count = loadout.SelectedLoadouts.TryGetValue(group.ID, out var picked) ? picked.Count : 0;
            var name = GroupName(group);
            var button = new Button
            {
                Text = count > 0 ? $"{name} ({count})" : name,
                ToggleMode = true,
                Pressed = group.ID == _group,
                HorizontalExpand = true,
                ClipText = true,
                StyleClasses = { StyleClass.ButtonSmall },
            };
            button.Label.Align = Label.AlignMode.Left;
            var id = group.ID;
            button.OnPressed += _ =>
            {
                _group = id;
                _search.Text = string.Empty;
                BuildCards();
                Refresh(profile, loadout, session);
            };
            role.Groups.AddChild(button);
        }
    }

    private string? Description(LoadoutPrototype proto)
    {
        var entity = proto.DummyEntity ?? _entities.System<LoadoutSystem>().GetFirstOrNull(proto);
        return entity != null && _prototypes.TryIndex(entity.Value, out var entityProto) ? entityProto.Description : null;
    }

    private void BuildCards()
    {
        _items.RemoveAllChildren();
        _cards.Clear();
        if (_group is not { } groupId || !_prototypes.TryIndex(groupId, out var groupProto))
            return;

        var loadoutSystem = _entities.System<LoadoutSystem>();
        foreach (var protoId in groupProto.Loadouts)
        {
            if (!_prototypes.TryIndex(protoId, out var proto))
                continue;

            var name = loadoutSystem.GetName(proto);
            var view = new EntityPrototypeView
            {
                Scale = new Vector2(2, 2),
                SetSize = new Vector2(64, 64),
                HorizontalAlignment = HAlignment.Center,
            };
            view.SetPrototype(proto.DummyEntity ?? loadoutSystem.GetFirstOrNull(proto));

            var card = new ContainerButton
            {
                SetSize = new Vector2(CardWidth, CardHeight),
                StyleClasses = { MarkingCard.StyleClassCard },
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        Margin = new Thickness(4),
                        SeparationOverride = 2,
                        Children =
                        {
                            view,
                            new Label { Text = name, ClipText = true, HorizontalAlignment = HAlignment.Center },
                        },
                    },
                },
            };
            var id = proto.ID;
            card.OnPressed += _ =>
            {
                if (card.HasStyleClass(MarkingCard.StyleClassPicked))
                    OnLoadoutUnpressed?.Invoke(groupId, id);
                else
                    OnLoadoutPressed?.Invoke(groupId, id);
            };

            _cards.Add((proto, card, name));
            _items.AddChild(card);
        }

        FilterItems();
    }

    private void FilterItems()
    {
        var search = _search.Text.Trim();
        foreach (var (_, card, name) in _cards)
            card.Visible = search.Length == 0 || name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    #endregion
}
