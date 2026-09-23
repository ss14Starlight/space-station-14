// ReSharper disable once CheckNamespace
namespace Content.Shared.TurretController;

public abstract partial class SharedDeployableTurretControllerSystem
{
    /// <summary>
    /// Returns whether the controller is restricted to displaying turret state.
    /// </summary>
    private static bool IsReadOnly(Entity<DeployableTurretControllerComponent> ent)
        => ent.Comp.ReadOnly;
}
