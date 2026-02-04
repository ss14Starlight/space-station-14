using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.DeviceLinking; // Starlight-edit

namespace Content.Shared.Silicons.Laws.Components;

/// <summary>
/// Whenever an entity is inserted with silicon laws it will update the relevant entity's laws.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState] // Starlight-edit
public sealed partial class SiliconLawUpdaterComponent : Component
{
    /// <summary>
    /// Entities to update
    /// </summary>
    //[DataField(required: true)] Starlight-edit: Changed to device linking
    //public ComponentRegistry Components; Starlight-edit: Changed to device linking
    
    [DataField, AutoNetworkedField]
    public EntityUid? Core; // Starlight-edit
}
