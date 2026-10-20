using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.PDA.Components;

/// <summary>
/// Marks a PDA whose ID card name should track its wearer's name on equip.
/// Only the ID it spawned with is renamed, so a swapped-in ID keeps its name.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SyncIdNameToWearerComponent : Component
{
    /// <summary>
    /// The ID card this PDA spawned with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? OriginalId;
}
