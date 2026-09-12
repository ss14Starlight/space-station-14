using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Marks a held item as a Starlight-style surgery tool for the tutorial BUI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialStarlightSurgeryToolComponent : Component
{
    [DataField, AutoNetworkedField]
    public TutorialStarlightSurgeryToolType ToolType;
}
