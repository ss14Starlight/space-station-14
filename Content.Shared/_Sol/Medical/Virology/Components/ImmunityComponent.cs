using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sol.Medical.Virology.Components;

/// <summary>
/// Tracks antibody titers / temporary immunity against pathogens.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImmunityComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ImmunityEntry> Entries = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ImmunityEntry
{
    /// <summary>
    /// Matches <see cref="PathogenPrototype.VaccineIdentity"/> or pathogen ID.
    /// </summary>
    [DataField]
    public string Identity = string.Empty;

    /// <summary>
    /// 0-1 resistance multiplier applied to infection chance (0 = full immunity).
    /// </summary>
    [DataField]
    public float Strength = 1f;

    [DataField]
    public TimeSpan ExpiresAt;
}
