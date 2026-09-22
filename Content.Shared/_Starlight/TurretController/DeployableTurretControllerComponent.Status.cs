namespace Content.Shared.TurretController;

public sealed partial class DeployableTurretControllerComponent
{
    /// <summary>
    /// Whether this controller only displays state and rejects configuration changes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ReadOnly;
}
