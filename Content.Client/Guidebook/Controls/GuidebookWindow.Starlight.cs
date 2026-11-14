using Content.Client._Starlight.Guidebook;
using Content.Client.UserInterface.Controls.FancyTree;
using Content.Shared.Decals;
using Content.Shared.Guidebook;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Guidebook.Controls;

/// <summary>
///     Starlight entry search and navigation colors for the guidebook.
/// </summary>
public sealed partial class GuidebookWindow
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ILocalizationManager _localizationManager = default!;

    private static readonly ProtoId<ColorPalettePrototype> DepartmentalPalette = "Departmental";

    /// <summary>
    ///     Localized search terms cached for the entries currently shown in the tree.
    /// </summary>
    private readonly Dictionary<TreeItem, GuidebookSearchEntry> _searchEntries = new();

    /// <summary>
    ///     Expansion states restored when the player clears a search.
    /// </summary>
    private readonly Dictionary<TreeItem, bool> _preSearchExpanded = new();

    /// <summary>
    ///     Resolved entry colors inherited by child pages without an explicit color or department.
    /// </summary>
    private readonly Dictionary<TreeItem, Color> _entryColors = new();

    /// <summary>
    ///     Connects the category search field to entry filtering.
    /// </summary>
    private void InitializeGuidebookSearch() => CategorySearchBar.OnTextChanged += _ => HandleCategorySearch();

    /// <summary>
    ///     Clears the query and restores hidden entries before navigation or a guidebook refresh.
    /// </summary>
    private void ClearCategorySearch() => CategorySearchBar.SetText("", invokeEvent: true);

    /// <summary>
    ///     Discards cached search terms, expansion states, and colors when the entry tree is rebuilt.
    /// </summary>
    private void ClearGuidebookSearchIndex()
    {
        _searchEntries.Clear();
        _preSearchExpanded.Clear();
        _entryColors.Clear();
    }

    /// <summary>
    ///     Caches an entry's search terms and resolves its explicit, departmental, or inherited color.
    /// </summary>
    private void IndexGuidebookEntry(TreeItem item, TreeItem? parent, GuideEntry entry)
    {
        _searchEntries[item] = GuidebookSearchEntry.FromEntry(entry, _localizationManager);

        var color = entry.Color;
        if (color == null && entry.Department is { } department)
            color = GetDepartmentColor(department);

        if (color == null && parent != null && _entryColors.TryGetValue(parent, out var parentColor))
            color = parentColor;

        if (color is { } entryColor)
        {
            _entryColors[item] = entryColor;
            ApplyEntryColor(item.Label, entryColor);
        }
    }

    /// <summary>
    ///     Uses departmental decal colors where available, falling back to the department's own color.
    /// </summary>
    private Color GetDepartmentColor(ProtoId<DepartmentPrototype> department)
    {
        var key = department.Id switch
        {
            "Civilian" => "service",
            "ITG" => "salvage",
            _ => department.Id.ToLowerInvariant(),
        };

        var palette = _prototypeManager.Index(DepartmentalPalette);
        return palette.Colors.TryGetValue(key, out var color)
            ? color.WithAlpha(1f)
            : _prototypeManager.Index(department).Color;
    }

    /// <summary>
    ///     Adds an entry's color marker and lightens dark text while preserving its hue and saturation.
    /// </summary>
    private static void ApplyEntryColor(Label label, Color color)
    {
        var textColor = color;
        if (Color.ToHsv(color).Z < 0.6f)
        {
            var hsl = Color.ToHsl(color);
            hsl.Z = 0.65f;
            textColor = Color.FromHsl(hsl);
        }

        label.FontColorOverride = textColor;
        var marker = new PanelContainer
        {
            SetWidth = 5,
            Margin = new Thickness(0, 3, 3, 3),
            PanelOverride = new StyleBoxFlat(color),
        };
        label.Parent!.AddChild(marker);
        marker.SetPositionInParent(label.GetPositionInParent());
    }

    /// <summary>
    ///     Filters guidebook entries and displays a message when no entry matches.
    /// </summary>
    private void HandleCategorySearch()
    {
        var query = CategorySearchBar.Text.Trim();

        if (query.Length == 0)
        {
            CategoryNoResultsLabel.Visible = false;
            foreach (var item in Tree.Items)
                item.Visible = true;

            foreach (var item in Tree.Items)
            {
                if (_preSearchExpanded.TryGetValue(item, out var expanded))
                    item.SetExpanded(expanded);
                item.UpdateIcon();
            }

            _preSearchExpanded.Clear();
            Tree.QueueRowStyleUpdate();
            return;
        }

        if (_preSearchExpanded.Count == 0)
        {
            foreach (var item in Tree.Items)
                _preSearchExpanded[item] = item.Expanded;
        }

        var anyVisible = false;
        foreach (var topLevel in Tree.Body.Children)
        {
            if (topLevel is not TreeItem item)
                continue;

            anyVisible |= FilterCategoryTree(item, query);
        }

        CategoryNoResultsLabel.Visible = !anyVisible;
        Tree.QueueRowStyleUpdate();
    }

    /// <summary>
    ///     Reveals matching pages and their ancestors, retaining all descendants of a matching parent.
    /// </summary>
    private bool FilterCategoryTree(TreeItem item, string query, bool parentMatches = false)
    {
        var matches = parentMatches || _searchEntries[item].Matches(query);
        var childMatches = false;

        foreach (var child in item.Body.Children)
        {
            if (child is TreeItem childItem)
                childMatches |= FilterCategoryTree(childItem, query, matches);
        }

        item.Visible = matches || childMatches;
        if (childMatches)
            item.SetExpanded(true);
        item.UpdateIcon();

        return item.Visible;
    }
}
