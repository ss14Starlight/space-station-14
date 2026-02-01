using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Antags.Vampires.Components.Classes;

/// <summary>
/// Marker component indicating Blood Swell is currently active
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveBloodSwellComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;
}
