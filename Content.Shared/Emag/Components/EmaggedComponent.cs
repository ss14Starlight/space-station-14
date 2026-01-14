using Content.Shared.Emag.Systems;
using Robust.Shared.GameStates;
using Content.Shared.NPC.Prototypes; // Starlight
using Robust.Shared.Prototypes; // Starlight

namespace Content.Shared.Emag.Components;

/// <summary>
/// Marker component for emagged entities
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmaggedComponent : Component
{
    /// <summary>
    /// The EmagType flags that were used to emag this device
    /// </summary>
    [DataField, AutoNetworkedField]
    public EmagType EmagType = EmagType.None;

    // Starlight begin
    /// <summary>
    /// The faction that emagged this device
    /// </summary>
    [DataField, AutoNetworkedField] public ProtoId<NpcFactionPrototype>? OwningFaction;
    // Starlight end
}
