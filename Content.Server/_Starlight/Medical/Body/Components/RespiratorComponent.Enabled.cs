namespace Content.Server.Body.Components;

public sealed partial class RespiratorComponent
{
    /// <summary>
    /// Whether breathing and the need for air are enabled.
    /// </summary>
    [DataField]
    public bool Enabled = true;
}
