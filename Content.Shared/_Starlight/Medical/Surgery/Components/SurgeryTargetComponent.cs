using Robust.Shared.GameStates;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
namespace Content.Shared.Starlight.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryTargetComponent : Component
{
    /// <summary>
    /// Determines should we bypass hygiene penalty like mask or gloves missing. For IPC for example.
    /// </summary>
    [DataField]
    public bool BypassHygienePenalty = false;
}
