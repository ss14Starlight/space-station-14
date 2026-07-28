namespace Content.Client._Moffstation.Interaction;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class InteractionParticleTrackerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoPausedField]
    public TimeSpan ExpireTime;
}
