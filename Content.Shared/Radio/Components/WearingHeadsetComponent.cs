using Robust.Shared.GameStates;

namespace Content.Shared.Radio.Components;

/// <summary>
///     This component is used to tag players that are currently wearing an ACTIVE headset.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Starlight edit
public sealed partial class WearingHeadsetComponent : Component
{
    [DataField, AutoNetworkedField] // Starlight edit 
    public EntityUid Headset;
}
