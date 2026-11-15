using System.Linq;

// ReSharper disable CheckNamespace
namespace Content.Client.UserInterface.Systems.Actions;

public sealed partial class ActionUIController
{
    /// <summary>
    /// Whether the bar needs comparing against the default order. Handled at most once per frame.
    /// </summary>
    private bool _sortQueued;

    /// <summary>
    /// Set once the player rearranges the bar (or a saved layout is loaded); we stop re-sorting so we don't undo it.
    /// </summary>
    private bool _userArranged;

    private void ResetArrangement()
    {
        _userArranged = false;
        _sortQueued = true;
    }

    private void QueueSort()
    {
        if (!_userArranged)
            _sortQueued = true;
    }

    private void AddToHotbar(EntityUid action)
    {
        if (_actionsSystem == null ||
            _container == null ||
            _actions.Contains(action) ||
            _actionsSystem.GetClientActions().All(clientAction => clientAction.Owner != action))
            return;

        _userArranged = true;
        _actions.Add(action);

        _container.SetActionData(_actionsSystem, _actions.ToArray());
    }
}
