using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Tracks sterility state of surgical tools and gloves.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgicalToolSterilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public SurgicalSterilityState State = SurgicalSterilityState.Sterile;

    [DataField, AutoNetworkedField]
    public List<PathogenContaminationEntry> Contaminants = new();

    /// <summary>
    /// Multiplier applied to surgery infection chance when this tool is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DirtyInfectionMultiplier = 2.5f;
}

[Serializable, NetSerializable]
public enum SurgicalSterilityState : byte
{
    Sterile = 0,
    Disinfected = 1,
    Dirty = 2,
}

/// <summary>
/// Worn surgical mask that reduces operator-to-patient droplet infection.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgicalMaskProtectionComponent : Component
{
    /// <summary>
    /// Multiplier applied to operator-carrier infection contribution (lower is better).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OperatorDropletMultiplier = 0.35f;
}
