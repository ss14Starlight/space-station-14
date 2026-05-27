using Content.Shared.Revolutionary;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Mindshield.Components;

/// <summary>
/// Component given to an entity to mark it is a mindshield implant.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRevolutionarySystem))]
public sealed partial class MindShieldImplantComponent : Component
{
    /// <summary>
    /// Stalright
    /// Icon of the Mindshield
    /// </summary>
    [DataField]
    public ProtoId<SecurityIconPrototype> MindShieldStatusIcon = "MindShieldIcon";
}
