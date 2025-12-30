using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.BreathOrgan.Components;

/// <summary>
/// A component giving organs the ability to be refilled.
/// The owner of the organ is being given a verb saying "Refill organ",
/// that will take all pressure it can to fill the organ up to targetPressure
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganGasTankFillableComponent : Component
{
    /// <summary>
    /// The target pressure to fill organ up to. defined in kPa
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TargetPressure = 1000f;
}
