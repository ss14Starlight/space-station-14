using Content.Shared.TurretController;

namespace Content.Client.TurretController;

public sealed partial class TurretControllerWindow
{
    /// <summary>
    /// Returns whether this window belongs to a read-only status panel.
    /// </summary>
    private bool IsReadOnly()
    {
        return _entManager.TryGetComponent<DeployableTurretControllerComponent>(_owner, out var controller) &&
            controller.ReadOnly;
    }
}
