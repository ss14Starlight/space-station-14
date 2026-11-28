using Robust.Shared.GameStates;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// Set player speed to zero and standing state to down, simulating leg paralysis.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(LegsParalyzedSystem))] // Starlight
public sealed partial class LegsParalyzedComponent : Component
{
    /// <summary>
    /// Allows crawling when unbuckled instead of completely blocking movement.
    /// </summary>
    [DataField, AutoNetworkedField] // Starlight
    public bool AllowCrawling;
}
