using Content.Shared.TurretController;

// ReSharper disable once CheckNamespace
namespace Content.Client.TurretController;

public sealed partial class TurretControllerWindow
{
    /// <summary>
    /// Returns whether this window belongs to a read-only status panel.
    /// </summary>
    private bool IsReadOnly()
        => _entManager.TryGetComponent<DeployableTurretControllerComponent>(_owner, out var controller) &&
            controller.ReadOnly;
}
