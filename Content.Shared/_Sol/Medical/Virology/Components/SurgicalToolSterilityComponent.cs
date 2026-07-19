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

    /// <summary>
    /// Surgical uses remaining before the tool becomes fully dirty / an infection risk.
    /// Attacking someone or completing the last clean use sets this to 0.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CleanUsesRemaining = 3;

    /// <summary>
    /// Clean-use budget restored by full sterilization.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxCleanUses = 3;

    [DataField, AutoNetworkedField]
    public List<PathogenContaminationEntry> Contaminants = new();

    /// <summary>
    /// Multiplier applied to surgery infection chance when this tool is used while Dirty.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DirtyInfectionMultiplier = 2.5f;

    /// <summary>
    /// When true, surgical uses and melee attacks never consume clean uses or mark this Dirty
    /// (e.g. bone gel bottles — sterility is about the gel, not the applicator bottle).
    /// Organs are also never marked Dirty (handled separately via OrganComponent checks).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PermanentSterility;
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
