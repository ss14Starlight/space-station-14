using System.Linq;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Shared.Actions.Components;
using Content.Shared.Input;
using Robust.Client.UserInterface;

// ReSharper disable CheckNamespace
namespace Content.Client.UserInterface.Systems.Actions;

public sealed partial class ActionUIController
{
    /// <summary>
    /// True from attaching to an entity until all of its actions have arrived. While set, the whole bar gets
    /// re-sorted whenever actions come in, instead of appending them in whatever order the client received them.
    /// </summary>
    private bool _settling;

    private bool _sortQueued;

    /// <summary>
    /// Set once the player rearranges the bar (or a saved layout is loaded); we stop re-sorting so we don't undo it.
    /// </summary>
    private bool _userArranged;

    private void StartSettling()
    {
        _userArranged = false;
        _settling = true;
    }

    private void QueueSortIfSettling()
    {
        if (_settling && !_userArranged)
            _sortQueued = true;
    }

    private bool AllActionsArrived()
    {
        if (_playerManager.LocalEntity is not { } user
            || !EntityManager.TryGetComponent<ActionsComponent>(user, out var actions))
            return false;

        return actions.Actions.All(action => EntityManager.HasComponent<ActionComponent>(action));
    }

    private void AddToHotbar(EntityUid action)
    {
        if (_actionsSystem == null || _actions.Contains(action))
            return;

        _userArranged = true;
        _actions.Add(action);

        _container?.SetActionData(_actionsSystem, _actions.ToArray());
    }

    /// <summary>
    /// Keyboard binds go to the keyboard-focused control (e.g. the search bar) instead of the hovered one,
    /// so quick-add bubbles up to the window; resolve the hovered action button from here.
    /// </summary>
    private void OnWindowKeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != ContentKeyFunctions.HotbarQuickAdd)
            return;

        var button = UIManager.CurrentlyHovered?.GetSelfAndLogicalAncestors()
            .OfType<ActionButton>()
            .FirstOrDefault(b => b.Parent == _window?.ResultsGrid);

        if (button != null)
            OnWindowActionPressed(args, button);
    }
}
