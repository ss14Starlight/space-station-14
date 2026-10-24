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

	// <BoxContainer Orientation="Horizontal">
	//  <Label ToolTip="{Loc 'ui-options-admin-ghost-script-tooltip'}" Text="{Loc 'ui-options-admin-ghost-script-label'}" HorizontalExpand="True" VerticalAlignment="Center" />
	//  <LineEdit ToolTip="{Loc 'ui-options-admin-ghost-script-tooltip'}" Name="AghostScriptName" MinWidth="240" />
	//  </BoxContainer>
	//  <HBox>
	//  <Label ToolTip="{Loc 'ui-options-admin-space-map-tooltip'}" Text="{Loc 'ui-options-admin-space-map-label'}" HorizontalExpand="True" VerticalAlignment="Center" />
	//  <LineEdit ToolTip="{Loc 'ui-options-admin-space-map-tooltip'}" Name="AdminArenaMapPath" MinWidth="240" />
	//  </HBox>
	//  <HBox>
	//  <Label ToolTip="{Loc 'ui-options-admin-space-map-init-tooltip'}" Text="{Loc 'ui-options-admin-space-map-init-label'}" HorizontalExpand="True" VerticalAlignment="Center" />
	//  <CheckBox ToolTip="{Loc 'ui-options-admin-space-map-init-tooltip'}" Name="AdminArenaMapInit" MinWidth="240" />
	//  </HBox>
	// Control.AddOptionLineEdit(StarlightCCVars.AdminGhostScriptPath, AghostScriptName);
	// Control.AddOptionLineEdit(StarlightCCVars.AdminArenaMapPath, AdminArenaMapPath);
	// Control.AddOptionCheckBox(StarlightCCVars.AdminArenaMapInit, AdminArenaMapInit);

    public void RefreshToolsVisibility()
    {
        AdminToolOptsContainer.RemoveAllChildren();
        RenderAdminTools();
    }

    private void RenderAdminTools() =>
        AdminToolOptsContainer.Visible = RenderAdminGhostTools() | RenderAdminArenaMapTools();

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
		Control.AddOptionLineEdit(StarlightCCVars.AdminGhostScriptPath, lineEdit);
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
		Control.AddOptionLineEdit(StarlightCCVars.AdminArenaMapPath, mapLineEdit);

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
		Control.AddOptionCheckBox(StarlightCCVars.AdminArenaMapInit, initCheckBox);
        return true;
    }
}
