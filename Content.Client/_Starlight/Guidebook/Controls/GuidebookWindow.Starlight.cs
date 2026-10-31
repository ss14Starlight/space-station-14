// ReSharper disable CheckNamespace
using Content.Client._Starlight.Stylesheets;
using Content.Client.Resources;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    [Dependency] private IResourceCache _resCache = default!;

    private const string CollapseIconPath = "/Textures/_Starlight/Interface/Nano/sidebar_collapse.svg.png";
    private const string ExpandIconPath = "/Textures/_Starlight/Interface/Nano/sidebar_expand.svg.png";

    private readonly TextureButton _sidebarButton = new()
    {
        StyleClasses = { StarlightStyleClass.WindowSidebarButton },
        VerticalAlignment = VAlignment.Center,
        Margin = new Thickness(0, 0, 4, 0),
        Visible = false,
    };

    private bool _hasSidebar;

    /// <summary>
    /// Where the divider was last dragged to.
    /// </summary>
    private float? _sidebarSplit;

    /// <summary>
    /// Whether the guide list is collapsed. Carried over to new windows by <c>GuidebookUIController</c>.
    /// </summary>
    public bool SidebarCollapsed
    {
        get;
        set
        {
            field = value;
            ApplySidebarCollapsed(value);
        }
    }

    private void InitializeSidebarToggle()
    {
        if (CloseButton.Parent is { } header)
        {
            header.AddChild(_sidebarButton);
            _sidebarButton.SetPositionFirst();
        }

        _sidebarButton.OnPressed += _ => SidebarCollapsed = !SidebarCollapsed;

        // The header doesn't go with the popout, so the sidebar couldn't be expanded there.
        OnPopout += () => ApplySidebarCollapsed(false);

        Split.OnSplitResizeFinished += () => _sidebarSplit = Split.SplitCenter;
        // Resized fires mid-arrange, where the split can't queue another layout pass.
        Split.OnResized += () => UserInterfaceManager.DeferAction(RestoreSidebarSplit);
    }

    private void RestoreSidebarSplit()
    {
        if (TreeBox.Visible && _sidebarSplit is { } split && Split.SplitCenter < split)
            Split.SplitCenter = split;
    }

    /// <summary>
    /// Shows the toggle when there is a guide list to collapse and applies <see cref="SidebarCollapsed"/>.
    /// </summary>
    private void SetSidebarAvailable(bool available)
    {
        _hasSidebar = available;
        _sidebarSplit = null;
        _sidebarButton.Visible = available;

        if (available)
            ApplySidebarCollapsed(SidebarCollapsed);
    }

    private void ApplySidebarCollapsed(bool collapsed)
    {
        if (!_hasSidebar)
            return;

        TreeBox.Visible = !collapsed;
        Split.ResizeMode = collapsed
            ? SplitContainer.SplitResizeMode.NotResizable
            : SplitContainer.SplitResizeMode.RespectChildrenMinSize;

        if (collapsed)
        {
            // A manual split keeps the sidebar's width as an empty gap.
            Split.State = SplitContainer.SplitState.Auto;
        }
        else if (_sidebarSplit is { } split)
        {
            Split.SplitCenter = split;
        }

        _sidebarButton.TextureNormal = _resCache.GetTexture(collapsed ? ExpandIconPath : CollapseIconPath);
        _sidebarButton.ToolTip = Loc.GetString(collapsed ? "guidebook-sidebar-expand" : "guidebook-sidebar-collapse");
    }
}
