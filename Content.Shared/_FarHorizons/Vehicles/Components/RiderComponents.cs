using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RiderComponent : Component
{
    /// <summary>
    /// The vehicle the person is controlling
    /// </summary>
    [DataField("riding")]
    public EntityUid? Riding;
    
}