using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.NameConfusion;

/// <summary>
/// Admeme component to periodically change an entity's name. Designed to be used with a command, but it's safe to use VV.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(NameConfusionSystem))]
public sealed partial class NameConfusionComponent : Component
{
    /// <summary>
    /// List of names to periodically pull from and rename the entity as.
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<string> Names = [];

    /// <summary>
    /// If it should confuse name after speaking.
    /// </summary>
    [DataField, AutoNetworkedField] public bool ConfuseOnSpeak;

    /// <summary>
    ///  If it should confuse name when being examined.
    /// </summary>
    [DataField, AutoNetworkedField] public bool ConfuseOnExamine;

    /// <summary>
    /// Enables confusing the name on an interval.
    /// </summary>
    [DataField, AutoNetworkedField] public bool ConfuseOnInterval;

    /// <summary>
    /// Probability that name gets confused.
    /// </summary>
    [DataField, AutoNetworkedField] public float NameConfusionProbability = 1;

    /// <summary>
    /// Probability that the name is restored on the next name confusion.
    /// </summary>
    [DataField, AutoNetworkedField] public float NameRestoreProbability = 1;

    /// <summary>
    /// Time to wait before confusing the name if <see cref="ConfuseOnInterval"/> is true.
    /// </summary>
    [DataField, AutoNetworkedField] public TimeSpan ConfuseInterval;

    /// <summary>
    /// The next time the name will be confused.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public TimeSpan NextConfuseTime;

    /// <summary>
    /// Currently selected name for name modifier event.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public string? CurrentName;

    /// <summary>
    /// The entity's name from before the name got confused, modifiers and all. This is to ensure consistent name color.
    /// </summary>
    [ViewVariables, AutoNetworkedField] public string? OriginalName;
}
