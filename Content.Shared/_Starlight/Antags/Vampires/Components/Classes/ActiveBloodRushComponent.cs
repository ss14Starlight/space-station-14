using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Antags.Vampires.Components.Classes;

/// <summary>
/// Marker component indicating Blood Rush is currently active
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveBloodRushComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;
}
