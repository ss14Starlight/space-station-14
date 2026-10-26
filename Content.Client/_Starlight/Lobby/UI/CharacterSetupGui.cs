using System.Linq;
using Content.Client.Lobby.UI.Loadouts;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Client.Lobby.UI;

public sealed partial class CharacterSetupGui
{
    private static readonly HashSet<string> _reviewNavigationControls =
    [
        "HeaderButton",
        "CMarkingCategoryButton",
        "SlotSelector",
        "LoadoutButton",
        "ShowClothes",
    ];

    private static readonly TimeSpan _readOnlyRefreshInterval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _readOnlyRefreshAccumulator;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Parts of the editor rebuild themselves outside of our control, so keep reapplying while reviewing.
        if (!_preferencesManager.ReviewingPending)
            return;

        _readOnlyRefreshAccumulator += TimeSpan.FromSeconds(args.DeltaSeconds);
        if (_readOnlyRefreshAccumulator < _readOnlyRefreshInterval)
            return;

        _readOnlyRefreshAccumulator = TimeSpan.Zero;
        ApplyReadOnly();
    }

    public void UpdatePendingReview()
    {
        PendingReviewBar.Visible = _preferencesManager.ReviewingPending;
        if (!_preferencesManager.ReviewingPending)
            return;

        var viewing = _preferencesManager.ViewingChanges
            ? "pending-preferences-review-viewing-pending"
            : "pending-preferences-review-viewing-current";
        PendingReviewLabel.Text = Loc.GetString("pending-preferences-review-label",
            ("admin", _preferencesManager.PendingPreferencesAdmin?.Username ?? string.Empty),
            ("viewing", Loc.GetString(viewing)));
        PendingToggleButton.Text = Loc.GetString(_preferencesManager.ViewingChanges
            ? "pending-preferences-review-show-current"
            : "pending-preferences-review-show-pending");

        ApplyReadOnly();
    }

    private void OnLoadoutWindowOpened(LoadoutWindow window)
    {
        if (_preferencesManager.ReviewingPending)
            SetReadOnly(window.ContentsContainer);
    }

    private void ApplyReadOnly()
    {
        _createNewCharacterButton.Disabled = true;
        SetReadOnly(CharEditor);
        SetReadOnly(JobPriorityEditor);

        if (_humanoidProfileEditor.LoadoutWindow is { IsOpen: true } loadoutWindow)
            SetReadOnly(loadoutWindow.ContentsContainer);

        foreach (var child in Characters.Children.SelectMany(picker => picker.Children)) SetReadOnly(child);
    }

    private static bool IsReviewNavigation(Control control) =>
        control switch
        {
            BoxContainer { Name: "TabContainer" } => true,
            ToggleLoadoutButton => true,
            _ => control.Name is { } name && _reviewNavigationControls.Contains(name),
        };

    private static void SetReadOnly(Control control)
    {
        if (IsReviewNavigation(control))
            return;

        switch (control)
        {
            // Disabling would hide which option is selected, so just stop them from being clicked.
            case BaseButton { ToggleMode: true } or BaseButton { Group: not null }:
                control.MouseFilter = MouseFilterMode.Ignore;
                break;
            case BaseButton button:
                button.Disabled = true;
                break;
            case LineEdit lineEdit:
                lineEdit.Editable = false;
                break;
            case TextEdit textEdit:
                textEdit.Editable = false;
                break;
            case Slider slider:
                slider.Disabled = true;
                break;
            case ItemList itemList:
                itemList.SelectMode = ItemList.ItemListSelectMode.None;
                break;
        }

        foreach (var child in control.Children)
            SetReadOnly(child);
    }
}
