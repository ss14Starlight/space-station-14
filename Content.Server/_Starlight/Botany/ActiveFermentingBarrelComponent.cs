namespace Content.Server._Starlight.Botany;

/// <summary>
/// On a barrel that is sealed with crops in it and room left, so it's fermenting.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveFermentingBarrelComponent : Component
{
    /// <summary>
    /// When the next crop ferments.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextFerment;
}
