using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons;

/// <summary>
/// Marks a handcuff item as belonging to a specific borg hand slot.
/// When the cuff is applied to a target, a fresh replacement is automatically spawned
/// in the borg's hand. When the target is uncuffed, the cuff returns to the borg
/// (or is deleted if the slot is already occupied by the replacement).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorgHandcuffComponent : Component
{
    /// <summary>The chassis entity that owns this hand slot.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? OwnerChassis;

    /// <summary>The hand slot ID string (e.g. "1234-hand-2") used to pick up into the correct slot.</summary>
    [DataField, AutoNetworkedField]
    public string? HandId;
}
