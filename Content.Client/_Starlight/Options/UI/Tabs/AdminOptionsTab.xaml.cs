using Content.Shared._Starlight.CCVar;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

// ReSharper disable CheckNamespace
namespace Content.Client.Options.UI.Tabs;

public sealed partial class AdminOptionsTab
{
    [Dependency] private ISharedAdminManager _admin = null!;
    [Dependency] private IPlayerManager _player = null!;

    private readonly HashSet<BaseOption> _options = [];

    public void RefreshToolsVisibility()
    {
        AdminToolOptsContainer.RemoveAllChildren();
        foreach (var option in _options) Control.RemoveOption(option);
        _options.Clear();
        RenderAdminTools();
    }

    private void RenderAdminTools() =>
        AdminToolsContainer.Visible = RenderAdminGhostTools() | RenderAdminArenaMapTools();

    private bool RenderAdminGhostTools()
    {
        if (!_admin.HasAdminFlag(_player.LocalSession!, AdminFlags.Fun)) return false;

        var hbox = new HBox();
        var label = new Label
        {
            Text = Loc.GetString("ui-options-admin-ghost-script-label"),
            ToolTip = Loc.GetString("ui-options-admin-ghost-script-tooltip"),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        };

        var lineEdit = new LineEdit
        {
            ToolTip = Loc.GetString("ui-options-admin-ghost-script-tooltip"),
            MinWidth = 240
        };
        hbox.AddChild(label);
        hbox.AddChild(lineEdit);
        AdminToolOptsContainer.AddChild(hbox);
        _options.Add(Control.AddOptionLineEdit(StarlightCCVars.AdminGhostScriptPath, lineEdit));
        return true;
    }

    private bool RenderAdminArenaMapTools()
    {
        if (!_admin.HasAdminFlag(_player.LocalSession!, AdminFlags.Mapping)) return false;
        var mapBox = new HBox();
        var mapLabel = new Label
        {
            Text = Loc.GetString("ui-options-admin-space-map-label"),
            ToolTip = Loc.GetString("ui-options-admin-space-map-tooltip"),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        };

        var mapLineEdit = new LineEdit
        {
            ToolTip = Loc.GetString("ui-options-admin-space-map-tooltip"),
            MinWidth = 240
        };
        mapBox.AddChild(mapLabel);
        mapBox.AddChild(mapLineEdit);
        AdminToolOptsContainer.AddChild(mapBox);
        _options.Add(Control.AddOptionLineEdit(StarlightCCVars.AdminArenaMapPath, mapLineEdit));

        var initBox = new HBox();
        var initLabel = new Label
        {
            Text = Loc.GetString("ui-options-admin-space-map-init-label"),
            ToolTip = Loc.GetString("ui-options-admin-space-map-init-tooltip"),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center
        };

        var initCheckBox = new CheckBox { ToolTip = Loc.GetString("ui-options-admin-space-map-init-tooltip") };
        initBox.AddChild(initLabel);
        initBox.AddChild(initCheckBox);
        AdminToolOptsContainer.AddChild(initBox);
        _options.Add(Control.AddOptionCheckBox(StarlightCCVars.AdminArenaMapInit, initCheckBox));
        return true;
    }
}
