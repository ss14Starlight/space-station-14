using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Genetics.Components;

/// <summary>
/// A collection of traits known to the genetics console, along with an ordering players can adjust.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneticsConsoleComponent : Component
{
    /// <summary>
    /// The known genes available at this console, ordered from top to bottom.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public List<EntityUid> Genes = new();
}
