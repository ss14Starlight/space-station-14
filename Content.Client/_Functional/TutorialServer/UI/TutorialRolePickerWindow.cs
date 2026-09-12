using System.Numerics;
using Content.Client._Starlight.TutorialServer;
using Content.Shared._Functional.TutorialServer;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Functional.TutorialServer.UI;

public sealed partial class TutorialRolePickerWindow : DefaultWindow // Starlight edit
{
    [Dependency] private IEntitySystemManager _entSys = default!; // Starlight

    public event Action<string, bool>? RoleSelected;
    public event Action? QuitPressed;

    private readonly BoxContainer _list;
    private string? _pendingStubRoleId;
    private readonly Label _confirmLabel;
    private readonly Button _confirmButton;
    private readonly Button _cancelConfirmButton;
    private readonly Button? _quitButton; // Starlight edit

    public TutorialRolePickerWindow()
    {
        IoCManager.InjectDependencies(this); // Starlight
        Title = Loc.GetString("tutorial-server-picker-title");
        MinSize = new Vector2(420, 480);

        _list = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        _confirmLabel = new Label
        {
            Text = Loc.GetString("tutorial-server-stub-confirm-text"),
            Visible = false,
            HorizontalExpand = true,
            MaxWidth = 400,
        };

        _confirmButton = new Button
        {
            Text = Loc.GetString("tutorial-server-stub-confirm-button"),
            Visible = false,
        };
        _confirmButton.OnPressed += _ =>
        {
            if (_pendingStubRoleId == null)
                return;
            RoleSelected?.Invoke(_pendingStubRoleId, true);
            HideConfirm();
        };

        _cancelConfirmButton = new Button
        {
            Text = Loc.GetString("tutorial-server-stub-cancel-button"),
            Visible = false,
        };
        _cancelConfirmButton.OnPressed += _ => HideConfirm();

        // Starlight begin
        if (_entSys.GetEntitySystem<TutorialSystem>().IsInTutorial)
        {
            _quitButton = new Button
            {
                Text = Loc.GetString("tutorial-server-picker-quit"),
                HorizontalExpand = true,
                StyleClasses = new StyleClassCollection("negative")
            };
            _quitButton.OnPressed += _ => QuitPressed?.Invoke();
        }

        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label { Text = Loc.GetString("tutorial-server-picker-subtitle") },
                new ScrollContainer { VerticalExpand = true, MinHeight = 360, Children = { _list }, },
                _confirmLabel,
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal, Children = { _confirmButton, _cancelConfirmButton },
                },
            }
        };

        if (_quitButton is not null) container.AddChild(_quitButton);

        ContentsContainer.AddChild(container);
        // Starlight end
    }

    public void Populate(List<TutorialRolePickerEntry> roles)
    {
        _list.DisposeAllChildren();
        string? lastCategory = null;
        string? lastSubCategory = null;

        foreach (var role in roles)
        {
            if (role.Category != lastCategory)
            {
                lastCategory = role.Category;
                lastSubCategory = null;
                _list.AddChild(new Label
                {
                    Text = role.Category,
                    StyleClasses = { "LabelHeading" },
                });
            }

            if (!string.IsNullOrEmpty(role.SubCategory) && role.SubCategory != lastSubCategory)
            {
                lastSubCategory = role.SubCategory;
                _list.AddChild(new Label
                {
                    Text = role.SubCategory,
                    StyleClasses = { "LabelHeading" },
                    Margin = new Thickness(16, 4, 0, 0),
                });
            }

            // Blocked wins the label; "unfinished" would send them off to wait for a fix.
            var label = role.BlockedForSpecies
                ? Loc.GetString("tutorial-server-picker-species-entry", ("name", role.DisplayName))
                : role.Stub
                    ? Loc.GetString("tutorial-server-picker-stub-entry", ("name", role.DisplayName))
                    : role.DisplayName;

            var button = new Button
            {
                Text = label,
                HorizontalExpand = true,
                Disabled = role.BlockedForSpecies,
                ModulateSelfOverride = role.Stub || role.BlockedForSpecies ? Color.Gray : null,
                ToolTip = role.BlockedForSpecies
                    ? Loc.GetString("tutorial-server-picker-species-tooltip")
                    : null,
                Margin = string.IsNullOrEmpty(role.SubCategory)
                    ? default
                    : new Thickness(16, 0, 0, 0),
            };

            var roleId = role.RoleId;
            var stub = role.Stub;
            button.OnPressed += _ =>
            {
                if (stub)
                {
                    _pendingStubRoleId = roleId;
                    _confirmLabel.Visible = true;
                    _confirmButton.Visible = true;
                    _cancelConfirmButton.Visible = true;
                }
                else
                {
                    RoleSelected?.Invoke(roleId, false);
                }
            };

            _list.AddChild(button);
        }
    }

    private void HideConfirm()
    {
        _pendingStubRoleId = null;
        _confirmLabel.Visible = false;
        _confirmButton.Visible = false;
        _cancelConfirmButton.Visible = false;
    }
}
