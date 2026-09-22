namespace Content.Shared.TurretController;

public abstract partial class SharedDeployableTurretControllerSystem
{
    /// <summary>
    /// Returns whether the controller is restricted to displaying turret state.
    /// </summary>
    private static bool IsReadOnly(Entity<DeployableTurretControllerComponent> ent)
    {
        return ent.Comp.ReadOnly;
    }
}
